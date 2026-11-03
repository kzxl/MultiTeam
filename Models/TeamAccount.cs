using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace MultiTeam.Models
{
    public enum PlatformType
    {
        Teams,
        Zalo,
        Telegram,
        Messenger,
        WhatsApp,
        Discord,
        Slack,
        Skype,
        GoogleChat,
        Custom
    }

    public enum AccountLifecycleState
    {
        Active,
        Ready,
        Suspended,
        Hibernated
    }

    public class TeamAccount : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private string _groupName = string.Empty;
        private PlatformType _platform = PlatformType.Teams;
        private string _customUrl = string.Empty;
        private string _tagColor = string.Empty;
        private bool _isMuted;
        private bool _isPinned;
        private int _unreadCount;
        private bool _isActive;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Initial));
            }
        }

        public string GroupName
        {
            get => string.IsNullOrWhiteSpace(_groupName) ? "Chung" : _groupName.Trim();
            set
            {
                _groupName = value;
                OnPropertyChanged();
            }
        }

        public PlatformType Platform
        {
            get => _platform;
            set
            {
                _platform = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PlatformDisplayName));
                OnPropertyChanged(nameof(PlatformColor));
                OnPropertyChanged(nameof(DisplayTagColor));
                OnPropertyChanged(nameof(DefaultUrl));
            }
        }

        public string CustomUrl
        {
            get => _customUrl;
            set
            {
                _customUrl = value;
                OnPropertyChanged();
                if (_platform == PlatformType.Custom)
                {
                    OnPropertyChanged(nameof(DefaultUrl));
                }
            }
        }

        public string TagColor
        {
            get => _tagColor;
            set
            {
                _tagColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTagColor));
            }
        }

        [JsonIgnore]
        public string DisplayTagColor => string.IsNullOrWhiteSpace(_tagColor) ? PlatformColor : _tagColor;

        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (_isMuted != value)
                {
                    _isMuted = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MuteStatusText));
                }
            }
        }

        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (_isPinned != value)
                {
                    _isPinned = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PinStatusText));
                }
            }
        }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonIgnore]
        public string DefaultUrl => _platform switch
        {
            PlatformType.Zalo => "https://chat.zalo.me",
            PlatformType.Telegram => "https://web.telegram.org",
            PlatformType.Messenger => "https://www.messenger.com",
            PlatformType.WhatsApp => "https://web.whatsapp.com",
            PlatformType.Discord => "https://discord.com/app",
            PlatformType.Slack => "https://app.slack.com/client",
            PlatformType.Skype => "https://web.skype.com",
            PlatformType.GoogleChat => "https://chat.google.com",
            PlatformType.Custom => string.IsNullOrWhiteSpace(_customUrl) ? "https://google.com" : _customUrl.Trim(),
            _ => "https://teams.microsoft.com"
        };

        [JsonIgnore]
        public string PlatformDisplayName => _platform switch
        {
            PlatformType.Zalo => "Zalo",
            PlatformType.Telegram => "Telegram",
            PlatformType.Messenger => "Messenger",
            PlatformType.WhatsApp => "WhatsApp",
            PlatformType.Discord => "Discord",
            PlatformType.Slack => "Slack",
            PlatformType.Skype => "Skype",
            PlatformType.GoogleChat => "Google Chat",
            PlatformType.Custom => "Custom Web",
            _ => "Teams"
        };

        [JsonIgnore]
        public string PlatformColor => _platform switch
        {
            PlatformType.Zalo => "#0068ff",
            PlatformType.Telegram => "#0284c7",
            PlatformType.Messenger => "#0084ff",
            PlatformType.WhatsApp => "#25d366",
            PlatformType.Discord => "#5865f2",
            PlatformType.Slack => "#e01e5a",
            PlatformType.Skype => "#00aff0",
            PlatformType.GoogleChat => "#00ac47",
            PlatformType.Custom => "#64748b",
            _ => "#4f46e5"
        };

        [JsonIgnore]
        public string MuteStatusText => _isMuted ? "🔇 Bật chuông" : "🔔 Tắt chuông";

        [JsonIgnore]
        public string PinStatusText => _isPinned ? "📌 Bỏ ghim tài khoản" : "📌 Ghim lên đầu";

        [JsonIgnore]
        public int UnreadCount
        {
            get => _unreadCount;
            set
            {
                if (_unreadCount != value)
                {
                    _unreadCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasUnread));
                }
            }
        }

        [JsonIgnore]
        public bool HasUnread => _unreadCount > 0;

        [JsonIgnore]
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public string Initial
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_name)) return "?";
                var trimmed = _name.Trim();
                return char.ToUpper(trimmed[0]).ToString();
            }
        }

        private AccountLifecycleState _lifecycleState = AccountLifecycleState.Ready;

        [JsonIgnore]
        public AccountLifecycleState LifecycleState
        {
            get => _lifecycleState;
            set
            {
                if (_lifecycleState != value)
                {
                    _lifecycleState = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSleeping));
                    OnPropertyChanged(nameof(LifecycleBadge));
                    OnPropertyChanged(nameof(LifecycleStatusText));
                }
            }
        }

        [JsonIgnore]
        public bool IsSleeping => _lifecycleState == AccountLifecycleState.Suspended || _lifecycleState == AccountLifecycleState.Hibernated;

        [JsonIgnore]
        public string LifecycleBadge => _lifecycleState switch
        {
            AccountLifecycleState.Suspended => "💤",
            AccountLifecycleState.Hibernated => "❄️",
            _ => string.Empty
        };

        [JsonIgnore]
        public string LifecycleStatusText => _lifecycleState switch
        {
            AccountLifecycleState.Active => "Đang mở",
            AccountLifecycleState.Suspended => "Đang ngủ (RAM thấp)",
            AccountLifecycleState.Hibernated => "Ngủ đông (0% RAM)",
            _ => "Sẵn sàng"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
