namespace KlikerBlocks;

internal static class WebsiteLinks
{
    public const string Home = "https://malyshrush.github.io/kliker-blocks-site/";
    public const string Community = "https://github.com/Malyshrush/kliker-blocks-site/discussions";

    public static void Open(IWin32Window owner, string address = Home)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(address)
                { UseShellExecute = true });
        }
        catch (Exception error)
        {
            MessageBox.Show(owner, UiLanguage.Format("Не удалось открыть сайт: {0}", error.Message));
        }
    }
}
