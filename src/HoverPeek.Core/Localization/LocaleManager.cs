using System.Globalization;

namespace HoverPeek.Core.Localization;

public static class LocaleManager
{
    public static void SetLanguage(string cultureName)
    {
        var culture = new CultureInfo(cultureName);
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
