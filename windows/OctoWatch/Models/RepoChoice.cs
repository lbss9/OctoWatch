using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OctoWatch;

public sealed class RepoChoice : INotifyPropertyChanged
{
    private bool _isSelected;

    public required string Owner { get; init; }
    public required string Name { get; init; }
    public string FullName => FeedMapper.FullName(Owner, Name);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
