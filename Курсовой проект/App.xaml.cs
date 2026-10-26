using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Курсовой_проект.Services;
using Курсовой_проект.ViewModels;

namespace Курсовой_проект;

public partial class App : Application
{
    private ServiceProvider? services;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        var collection = new ServiceCollection();
        collection.AddSingleton<UserSession>();
        collection.AddSingleton<NotificationService>();
        collection.AddSingleton<MaintenanceReportService>();
        collection.AddSingleton<FleetReportService>();
        collection.AddSingleton<VehicleHistoryService>();
        collection.AddSingleton<AccessService>();
        collection.AddSingleton<IUserStore, UserStore>();
        collection.AddSingleton<IAuthenticationService, AuthenticationService>();
        collection.AddSingleton<IRegistrationStore, RegistrationStore>();
        collection.AddSingleton<IRegistrationService, RegistrationService>();
        collection.AddSingleton<IFleetService, FleetService>();
        collection.AddSingleton<IVehicleStore, VehicleStore>();
        collection.AddSingleton<ICatalogService, CatalogService>();
        collection.AddSingleton<ICatalogEditService, CatalogEditService>();
        collection.AddTransient<CatalogViewModel>();
        collection.AddTransient<LoginViewModel>();
        collection.AddTransient<RegisterViewModel>();
        collection.AddTransient<RegisterWindow>();
        collection.AddTransient<MainViewModel>();
        collection.AddTransient<LoginWindow>();
        collection.AddTransient<MainWindow>();
        services = collection.BuildServiceProvider();
        ShowLogin();
    }

    private void ShowLogin()
    {
        var provider = services ?? throw new InvalidOperationException("Сервисы приложения не настроены.");
        var login = provider.GetRequiredService<LoginWindow>();
        login.RegistrationRequested += (_, _) =>
        {
            var registration = provider.GetRequiredService<RegisterWindow>();
            registration.Owner = login;
            if (registration.ShowDialog() == true)
                login.CompleteRegistration(registration.RegisteredLogin);
        };
        login.Authenticated += (_, _) =>
        {
            var window = provider.GetRequiredService<MainWindow>();
            window.LogoutRequested += (_, _) =>
            {
                provider.GetRequiredService<UserSession>().SignOut();
                ShowLogin();
                window.Close();
            };
            MainWindow = window;
            window.Show();
            login.Close();
        };
        MainWindow = login;
        login.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        services?.GetRequiredService<UserSession>().SignOut();
        services?.Dispose();
        base.OnExit(e);
    }
}
