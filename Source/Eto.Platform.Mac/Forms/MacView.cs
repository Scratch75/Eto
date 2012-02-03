using System;
using MonoMac.AppKit;
using Eto.Drawing;
using Eto.Forms;
using MonoMac.CoreGraphics;
using MonoMac.Foundation;
using Eto.Platform.Mac.Drawing;
using MonoMac.ObjCRuntime;
using SD = System.Drawing;

namespace Eto.Platform.Mac
{
	class MouseDelegate : NSObject
	{
		public NSView View { get; set; }
	
		public Control Widget { get; set; }
	
		[Export("mouseMoved:")]
		public void MouseMoved (NSEvent theEvent)
		{
			Widget.OnMouseMove (Generator.GetMouseEvent (View, theEvent));
		}
		
		[Export("mouseEntered:")]
		public void MouseEntered (NSEvent theEvent)
		{
			Widget.OnMouseEnter (Generator.GetMouseEvent (View, theEvent));
		}

		[Export("cursorUpdate::")]
		public void CursorUpdate (NSEvent theEvent)
		{
		}
	
		[Export("mouseExited:")]
		public void MouseExited (NSEvent theEvent)
		{
			Widget.OnMouseLeave (Generator.GetMouseEvent (View, theEvent));
		}
	}
	
	public interface IMacAutoSizing
	{
		bool AutoSize { get; }
		
		void SizeToFit ();
		
	}

	public interface IMacView : IMacAutoSizing
	{
		Size PositionOffset { get; }
		
		Size? PreferredSize { get; }
		
		Size? MinimumSize { get; set; }
		
		Control Widget { get; }
		
		Cursor Cursor { get; set; }

	}
	
	public abstract class MacView<T, W> : MacObject<T, W>, IControl, IMacView
		where T: NSView
		where W: Control
	{
		bool focus;
		NSTrackingArea tracking;
		bool mouseMove;
		NSTrackingAreaOptions mouseOptions;
		MouseDelegate mouseDelegate;
		Cursor cursor;

		public virtual bool AutoSize { get; protected set; }

		public virtual Size Size {
			get { return Generator.ConvertF (Control.Frame.Size); }
			set { 
				this.PreferredSize = value;
				Generator.SetSizeWithAuto(Control, value);
				this.AutoSize = false;
				CreateTracking ();
				LayoutIfNeeded();
			}
		}
		
		protected void LayoutIfNeeded()
		{
			if (Widget.Loaded) {
				var layout = Widget.ParentLayout.Handler as IMacLayout;
				if (layout != null) layout.UpdateParentLayout();
			}
		}
		
		public virtual Size? MinimumSize {
			get { return null; }
			set { }
		}
		
		public virtual Size? MaximumSize {
			get;
			set;
		}
		
		public Size? PreferredSize {
			get;
			set;
		}
		
		public MacView ()
		{
 			this.AutoSize = true;
		}
		
		protected virtual void SetNaturalSize()
		{
			var control = Control as NSControl;
			if (control != null)
				control.SizeToFit ();
		}
		
		public virtual void SizeToFit ()
		{
			if (!AutoSize) {
				if (PreferredSize != null) {
					var preferredSize = PreferredSize.Value;
					if (preferredSize.Height == -1 || preferredSize.Width == -1) {
						SetNaturalSize();
						Generator.SetSizeWithAuto (Control, preferredSize);
					}
					else 
						Control.SetFrameSize (Generator.ConvertF (PreferredSize.Value));
				}
			} else
				SetNaturalSize ();

			if (MinimumSize != null || MaximumSize != null) {
				var size = Control.Frame.Size;
				if (MinimumSize != null) {
					size.Width = Math.Max (size.Width, MinimumSize.Value.Width);
					size.Height = Math.Max (size.Height, MinimumSize.Value.Height);
				}
				if (MaximumSize != null) {
					size.Width = Math.Min (size.Width, MaximumSize.Value.Width);
					size.Height = Math.Min (size.Height, MaximumSize.Value.Height);
				}
				Control.SetFrameSize (size);
			}
		}
		
		public virtual Size PositionOffset { get { return Size.Empty; } }
		
		void CreateTracking ()
		{
			if (!mouseMove)
				return;
			if (tracking != null)
				Control.RemoveTrackingArea (tracking);
			//Console.WriteLine ("Adding mouse tracking {0} for area {1}", this.Widget.GetType ().FullName, Control.Frame.Size);
			mouseDelegate = new MouseDelegate{ Widget = this.Widget, View = Control };
			tracking = new NSTrackingArea (new SD.RectangleF (new SD.PointF (0, 0), Control.Frame.Size), 
				NSTrackingAreaOptions.ActiveAlways | mouseOptions | NSTrackingAreaOptions.EnabledDuringMouseDrag | NSTrackingAreaOptions.InVisibleRect, 
			    mouseDelegate, 
				new NSDictionary ());
			Control.AddTrackingArea (tracking);
		}

		public virtual void SetParentLayout (Layout layout)
		{
		}
		
		public virtual void SetParent (Control parent)
		{
		}
		
		public override void AttachEvent (string handler)
		{
			switch (handler) {
			case Eto.Forms.Control.MouseEnterEvent:
			case Eto.Forms.Control.MouseLeaveEvent:
				HandleEvent (Eto.Forms.Control.SizeChangedEvent);
				mouseOptions |= NSTrackingAreaOptions.MouseEnteredAndExited;
				mouseMove = true;
				HandleEvent (Eto.Forms.Control.SizeChangedEvent);
				CreateTracking ();
				break;
			case Eto.Forms.Control.MouseMoveEvent:
				mouseOptions |= NSTrackingAreaOptions.MouseMoved;
				mouseMove = true;
				HandleEvent (Eto.Forms.Control.SizeChangedEvent);
				CreateTracking ();
				break;
			case Eto.Forms.Control.SizeChangedEvent:
				Control.PostsFrameChangedNotifications = true;
				this.AddObserver (NSView.NSViewFrameDidChangeNotification, delegate(ObserverActionArgs e) {
					((MacView<T, W>)(e.Widget.Handler)). OnSizeChanged (EventArgs.Empty);
					e.Widget.OnSizeChanged (EventArgs.Empty);
				});
				break;
			case Eto.Forms.Control.MouseDoubleClickEvent:
			case Eto.Forms.Control.MouseDownEvent:
			case Eto.Forms.Control.MouseUpEvent:
			case Eto.Forms.Control.ShownEvent:
			case Eto.Forms.Control.HiddenEvent:
			case Eto.Forms.Control.KeyDownEvent:
			case Eto.Forms.Control.LostFocusEvent:
			case Eto.Forms.Control.GotFocusEvent:
				// TODO
				break;
			default:
				base.AttachEvent (handler);
				break;

			}
		}
		
		protected virtual void OnSizeChanged (EventArgs e)
		{
			CreateTracking ();
		}
		
		public void Invalidate ()
		{
			Control.NeedsDisplay = true;
		}

		public void Invalidate (Rectangle rect)
		{
			var region = Generator.ConvertF (rect);
			region.Y = Control.Frame.Height - region.Y - region.Height;
			Control.SetNeedsDisplayInRect (region);
		}

		public Graphics CreateGraphics ()
		{
			return new Graphics (Widget.Generator, new GraphicsHandler (Control));
		}

		public void SuspendLayout ()
		{
		}

		public void ResumeLayout ()
		{
		}

		public void Focus ()
		{
			if (Control.Window != null)
				Control.Window.MakeFirstResponder (Control);
			else
				focus = true;
		}

		public virtual Color BackgroundColor {
			get { 
				if (!Control.WantsLayer) {
					Control.WantsLayer = true;
				}
				return Generator.Convert (Control.Layer.BackgroundColor);
			}
			set {
				if (!Control.WantsLayer) {
					Control.WantsLayer = true;
				}
				Control.Layer.BackgroundColor = Generator.Convert (value);
			}
		}

		public abstract bool Enabled { get; set; }

		public virtual bool HasFocus {
			get {
				return Control.Window != null && Control.Window.FirstResponder == ControlObject;
			}
		}

		public bool Visible {
			get { return !Control.Hidden; }
			set { Control.Hidden = !value; }
		}
		
		public Cursor Cursor {
			get { return cursor; }
			set { cursor = value; }
		}
		
		public string ToolTip {
			get { return Control.ToolTip; }
			set { Control.ToolTip = value; }
		}
		
		public virtual void OnPreLoad (EventArgs e)
		{
		}
		
		public virtual void OnLoad (EventArgs e)
		{
		}
		
		public virtual void OnLoadComplete (EventArgs e)
		{
			if (focus)
				Control.Window.MakeFirstResponder (Control);
		}
		
		#region IMacView implementation

		Control IMacView.Widget {
			get {
				return this.Widget;
			}
		}
		
		#endregion
	}
}

