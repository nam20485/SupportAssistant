using Avalonia.Controls;
using SupportAssistant.ViewModels;

namespace SupportAssistant.Views;

public partial class OnboardingWizardWindow : Window
{
    public OnboardingWizardWindow()
    {
        InitializeComponent();
        
        // Handle wizard completion
        DataContextChanged += (sender, e) =>
        {
            if (DataContext is OnboardingWizardViewModel vm)
            {
                vm.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(OnboardingWizardViewModel.IsCompleted) && vm.IsCompleted)
                    {
                        Close();
                    }
                };
            }
        };
    }
}
