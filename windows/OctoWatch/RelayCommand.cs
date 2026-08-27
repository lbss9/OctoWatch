using System;
using System.Windows.Input;

namespace OctoWatch;

/// <summary>ICommand mínimo para ligar ações (ex.: clique no ícone da bandeja).</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;

    public RelayCommand(Action execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();
}
