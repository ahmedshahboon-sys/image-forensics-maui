namespace ImageForensics.App;

public sealed class App : Application
{
    public App(MainPage mainPage)
    {
        UserAppTheme = AppTheme.Unspecified;
        MainPage = new NavigationPage(mainPage);
    }
}
