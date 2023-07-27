namespace Roost.Piebald
{
    public abstract class AbstractMetaViewWindow<TWindowHost> : AbstractViewWindow<TWindowHost>, IMetaWindow
        where TWindowHost : class, IWindowViewHost<TWindowHost>
    {
        protected AbstractMetaViewWindow()
            : base(true)
        {
        }
    }
}
