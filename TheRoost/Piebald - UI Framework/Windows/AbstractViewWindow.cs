namespace Roost.Piebald
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// A window that can contain and manage a stack of <see cref="IWindowView{TWindowHost}"/>s.
    /// </summary>
    /// <remarks>
    /// When deriving this class, you must also provide an interface derived from <see cref="IWindowViewHost{TWindowHost}"/> and
    /// implement it onto your derived class.  This will provide the API to manipulate your window from the views.
    /// </remarks>
    public abstract class AbstractViewWindow<TWindowHost> : AbstractWindow, IWindowViewHost<TWindowHost>
        where TWindowHost : class, IWindowViewHost<TWindowHost>
    {
        private IWindowView<TWindowHost> view;
        private IWindowView<TWindowHost> persistedView;

        private Stack<IWindowView<TWindowHost>> viewStack = new Stack<IWindowView<TWindowHost>>();

        WidgetMountPoint IWindowViewHost<TWindowHost>.Content => this.Content;

        WidgetMountPoint IWindowViewHost<TWindowHost>.Footer => this.Footer;

        protected abstract string DefaultTitle { get; }

        protected virtual Sprite DefaultIcon { get; } = null;

        protected virtual IWindowView<TWindowHost> DefaultView { get; } = null;

        protected virtual bool PersistViewOnClose { get; } = false;

        protected AbstractViewWindow(bool includeShadow)
            : base(includeShadow)
        {
        }

        protected IWindowView<TWindowHost> View
        {
            get
            {
                return this.view;
            }

            set
            {
                if (!this.IsVisible)
                {
                    this.persistedView = value;
                    return;
                }

                this.DetatchView();

                this.view = value;

                if (this.IsVisible)
                {
                    this.AttachView();
                }
            }
        }

        void IWindowViewHost<TWindowHost>.ReplaceView(IWindowView<TWindowHost> view)
        {
            this.ReplaceView(view);
        }

        void IWindowViewHost<TWindowHost>.PushView(IWindowView<TWindowHost> view)
        {
            this.PushView(view);
        }

        void IWindowViewHost<TWindowHost>.PopView()
        {
            this.PopView();
        }

        protected void ReplaceView(IWindowView<TWindowHost> view)
        {
            this.View = view;
        }

        protected void PushView(IWindowView<TWindowHost> view)
        {
            if (this.View != null)
            {
                this.viewStack.Push(this.View);
            }

            this.View = view;
        }

        protected void PopView()
        {
            if (this.viewStack.Count > 0)
            {
                this.View = this.viewStack.Pop();
            }
            else
            {
                this.View = this.DefaultView;
            }
        }

        protected override void OnOpen()
        {
            base.OnOpen();

            if (this.view == null)
            {
                this.view = this.persistedView ?? this.DefaultView;
            }

            this.persistedView = null;

            this.AttachView();
        }

        protected override void OnClose()
        {
            base.OnClose();

            if (this.PersistViewOnClose)
            {
                this.persistedView = this.view;
            }

            this.DetatchView();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            this.view?.Update();
        }

        private void DetatchView()
        {
            if (this.view != null)
            {
                this.view.Detatch();
                this.view = null;
            }

            this.Icon.Clear();
            this.Content.Clear();
            this.Footer.Clear();
        }

        private void AttachView()
        {
            this.Icon.Clear();
            this.Content.Clear();
            this.Footer.Clear();

            if (this.view is IViewHasIcon iconView && iconView.Icon != null)
            {
                this.Icon.AddImage("Icon")
                    .SetSprite(iconView.Icon);
            }
            else if (this.DefaultIcon)
            {
                this.Icon.AddImage("Icon")
                    .SetSprite(this.DefaultIcon);
            }

            if (this.view is IViewHasTitle titleView && !string.IsNullOrEmpty(titleView.Title))
            {
                this.Title = titleView.Title;
            }
            else if (!string.IsNullOrEmpty(this.DefaultTitle))
            {
                this.Title = this.DefaultTitle;
            }

            if (this.view != null)
            {
                this.view.Attach(this as TWindowHost);
            }
        }
    }
}
