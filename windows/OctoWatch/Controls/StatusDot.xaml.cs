using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace OctoWatch.Controls;

/// <summary>
/// Bolinha de status: verde (passou), vermelho (erro), amarelo pulsante
/// (em execução), cinza (desconhecido/outro).
/// </summary>
public sealed partial class StatusDot : UserControl
{
    public StatusDot()
    {
        this.InitializeComponent();
    }

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(string),
        typeof(StatusDot),
        new PropertyMetadata("other", OnStateChanged)
    );

    /// <summary>success | failure | running | other</summary>
    public string State
    {
        get => (string)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((StatusDot)d).Apply((string)(e.NewValue ?? "other"));
    }

    private void Apply(string state)
    {
        Pulse.Stop();

        Color color = state switch
        {
            "success" => Color.FromArgb(0xFF, 0x2E, 0xA0, 0x43), // verde
            "failure" => Color.FromArgb(0xFF, 0xD6, 0x3B, 0x3B), // vermelho
            "running" => Color.FromArgb(0xFF, 0xE3, 0xB3, 0x41), // amarelo
            _ => Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A), // cinza
        };

        var brush = new SolidColorBrush(color);
        Dot.Fill = brush;
        Halo.Fill = brush;

        if (state == "running")
        {
            Halo.Opacity = 0.55;
            Pulse.Begin();
        }
        else
        {
            Halo.Opacity = 0;
        }
    }
}
