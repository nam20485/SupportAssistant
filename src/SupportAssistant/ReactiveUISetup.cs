using System.Runtime.CompilerServices;
using ReactiveUI.Builder;

namespace SupportAssistant;

internal static class ReactiveUISetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        RxAppBuilder.CreateReactiveUIBuilder()
            .WithPlatformServices()
            .Build();
    }
}
