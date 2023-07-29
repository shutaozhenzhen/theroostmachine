namespace Roost.Piebald
{
    public abstract class AbstractTableViewWindow<TWindowHost> : AbstractViewWindow<TWindowHost>, ITableWindow
        where TWindowHost : class, IWindowViewHost<TWindowHost>
    {
        protected AbstractTableViewWindow()
            : base(true)
        {
        }
    }
}
