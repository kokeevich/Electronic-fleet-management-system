using System.Configuration;
using System.Data;
using System.Windows;

namespace Курсовой_проект
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private Microsoft.Extensions.DependencyInjection.ServiceProvider? services;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var collection = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                .AddSingleton<Services.IFleetService, Services.FleetService>(collection);
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                .AddTransient<ViewModels.MainViewModel>(collection);
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                .AddTransient<MainWindow>(collection);
            services = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions
                .BuildServiceProvider(collection);
            MainWindow = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<MainWindow>(services);
            MainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            services?.Dispose();
            base.OnExit(e);
        }
    }

}
