using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MultiTeam.Models
{
    public class AccountGroup : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private bool _isExpanded = true;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ArrowIcon));
                }
            }
        }

        public string ArrowIcon => _isExpanded ? "▼" : "▶";

        public ObservableCollection<TeamAccount> Accounts { get; } = new();

        public int TotalUnreadCount => Accounts.Sum(a => a.UnreadCount);
        public bool HasUnread => TotalUnreadCount > 0;
        public string CountText => $"({Accounts.Count})";

        public void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(TotalUnreadCount));
            OnPropertyChanged(nameof(HasUnread));
            OnPropertyChanged(nameof(CountText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
