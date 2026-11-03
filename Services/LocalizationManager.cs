using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MultiTeam.Services
{
    public class LocalizationManager : INotifyPropertyChanged
    {
        private static LocalizationManager? _instance;
        public static LocalizationManager Instance => _instance ??= new LocalizationManager();

        private string _currentLanguage = "vi";
        private readonly string _settingsFilePath;

        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    SaveLanguagePreference();
                    OnPropertyChanged();
                    OnPropertyChanged("Item[]");
                    OnPropertyChanged(nameof(LanguageToggleText));
                }
            }
        }

        public string LanguageToggleText => _currentLanguage == "vi" ? "🇻🇳 Tiếng Việt" : "🇬🇧 English";

        public string this[string key]
        {
            get
            {
                if (Translations.TryGetValue(_currentLanguage, out var langDict) && langDict.TryGetValue(key, out var text))
                {
                    return text;
                }
                if (Translations["en"].TryGetValue(key, out var enText))
                {
                    return enText;
                }
                return key;
            }
        }

        public LocalizationManager()
        {
            _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sessions", "settings.json");
            LoadLanguagePreference();
        }

        public void ToggleLanguage()
        {
            CurrentLanguage = (CurrentLanguage == "vi") ? "en" : "vi";
        }

        private void LoadLanguagePreference()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Language", out var langProp))
                    {
                        string? lang = langProp.GetString();
                        if (!string.IsNullOrEmpty(lang) && (lang == "vi" || lang == "en"))
                        {
                            _currentLanguage = lang;
                        }
                    }
                }
            }
            catch { }
        }

        private void SaveLanguagePreference()
        {
            try
            {
                string dir = Path.GetDirectoryName(_settingsFilePath)!;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonSerializer.Serialize(new { Language = _currentLanguage }, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch { }
        }

        private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
        {
            ["vi"] = new Dictionary<string, string>
            {
                ["AppTitle"] = "MultiTeam Hub - Trình Quản Lý Chat Đa Nền Tảng",
                ["BrandSubtitle"] = "Teams • Zalo • Tele • Messenger • WhatsApp...",
                ["BtnAddAccount"] = "Thêm tài khoản mới",
                ["CreateProfileTitle"] = "Tạo Profile Chat Mới",
                ["CreateProfileSubtitle"] = "Chọn nền tảng, phân nhóm và gán màu nhận diện để quản lý khoa học.",
                ["ChoosePlatform"] = "1. Chọn nền tảng chat:",
                ["AccountNameLabel"] = "2. Tên gợi nhớ tài khoản:",
                ["ColorTagLabel"] = "3. Đánh dấu màu nhận diện (Tag Color):",
                ["SectionGroup"] = "4. Phân nhóm / Workspace:",
                ["CustomUrlHint"] = "Địa chỉ trang web chat (https://...):",
                ["GroupHint"] = "Nhập hoặc chọn nhóm (vd: Công ty, Bán hàng, Cá nhân)...",
                ["DefaultGroup"] = "Chung",
                ["Cancel"] = "Hủy",
                ["Create"] = "Tạo Profile",
                ["SearchHint"] = "🔍 Tìm tài khoản (Ctrl+F)...",
                ["NoAccountSelected"] = "Chưa có tài khoản nào được chọn",
                ["NoAccountGuide"] = "Vui lòng bấm 'Thêm tài khoản mới' ở cột bên trái để bắt đầu.",
                ["Ready"] = "(Sẵn sàng)",
                ["Loading"] = "(Đang tải...)",
                ["Initializing"] = "(Đang khởi tạo...)",
                ["ConnectionError"] = "(Lỗi kết nối)",
                ["MuteOn"] = "🔇 Bật chuông",
                ["MuteOff"] = "🔔 Tắt chuông",
                ["MemorySaverOn"] = "⚡ Tiết kiệm RAM: Bật",
                ["MemorySaverOff"] = "⚡ Tiết kiệm RAM: Tắt",
                ["Reload"] = "🔄 Tải lại",
                ["Home"] = "🏠 Trang chủ",
                ["SessionsFolder"] = "📁 Thư mục Sessions",
                ["PinTop"] = "📌 Ghim lên đầu",
                ["Unpin"] = "📌 Bỏ ghim tài khoản",
                ["ClearCache"] = "🧹 Xóa Cache & Đăng xuất tài khoản này",
                ["Rename"] = "Đổi tên...",
                ["ChangeColorTag"] = "🎨 Đổi màu nhận diện...",
                ["MoveToGroup"] = "📁 Chuyển vào nhóm...",
                ["OpenFolder"] = "Mở thư mục dữ liệu",
                ["Delete"] = "Xóa tài khoản này",
                ["ConfirmDeleteTitle"] = "Xác nhận xóa tài khoản",
                ["ConfirmDeleteMsg"] = "Bạn có chắc chắn muốn xóa tài khoản '{0}' ({1}) không?\n(Dữ liệu phiên làm việc vẫn được lưu trong thư mục Sessions nếu bạn muốn dùng lại).",
                ["ConfirmClearCacheTitle"] = "Xác nhận làm mới phiên",
                ["ConfirmClearCacheMsg"] = "Bạn có chắc chắn muốn xóa Cache, Cookie và đăng xuất khỏi tài khoản '{0}' ({1}) không?",
                ["ClearCacheSuccess"] = "Đã xóa sạch dữ liệu phiên cho tài khoản '{0}'. Vui lòng đăng nhập lại.",
                ["NewMessage"] = "Tin nhắn mới",
                ["OneNewMessage"] = "Bạn có 1 tin nhắn mới.",
                ["MultipleNewMessages"] = "Bạn có {0} tin nhắn mới ({1} chưa đọc).",
                ["EnterAccountNameWarning"] = "Vui lòng nhập tên tài khoản.",
                ["EnterCustomUrlWarning"] = "Vui lòng nhập đường dẫn URL hợp lệ bắt đầu bằng https://",
                ["RenameTitle"] = "Đổi tên tài khoản",
                ["RenameSubtitle"] = "Nhập tên mới cho tài khoản này:",
                ["SaveChange"] = "Lưu thay đổi"
            },
            ["en"] = new Dictionary<string, string>
            {
                ["AppTitle"] = "MultiTeam Hub - Multi-Platform Chat Workspace",
                ["BrandSubtitle"] = "Teams • Zalo • Tele • Messenger • WhatsApp...",
                ["BtnAddAccount"] = "Add New Account",
                ["CreateProfileTitle"] = "Create New Chat Profile",
                ["CreateProfileSubtitle"] = "Select platform, group, and color tag for organized management.",
                ["ChoosePlatform"] = "1. Choose chat platform:",
                ["AccountNameLabel"] = "2. Account name / Alias:",
                ["ColorTagLabel"] = "3. Assign color tag:",
                ["SectionGroup"] = "4. Group / Workspace:",
                ["CustomUrlHint"] = "Chat Website URL (https://...):",
                ["GroupHint"] = "Enter or choose group (e.g. Work, Sales, Personal)...",
                ["DefaultGroup"] = "General",
                ["Cancel"] = "Cancel",
                ["Create"] = "Create Profile",
                ["SearchHint"] = "🔍 Search accounts (Ctrl+F)...",
                ["NoAccountSelected"] = "No account selected",
                ["NoAccountGuide"] = "Please click 'Add New Account' on the left sidebar to get started.",
                ["Ready"] = "(Ready)",
                ["Loading"] = "(Loading...)",
                ["Initializing"] = "(Initializing...)",
                ["ConnectionError"] = "(Connection Error)",
                ["MuteOn"] = "🔇 Unmute",
                ["MuteOff"] = "🔔 Mute",
                ["MemorySaverOn"] = "⚡ Memory Saver: ON",
                ["MemorySaverOff"] = "⚡ Memory Saver: OFF",
                ["Reload"] = "🔄 Reload",
                ["Home"] = "🏠 Home",
                ["SessionsFolder"] = "📁 Sessions Folder",
                ["PinTop"] = "📌 Pin to Top",
                ["Unpin"] = "📌 Unpin Account",
                ["ClearCache"] = "🧹 Clear Cache & Logout",
                ["Rename"] = "Rename...",
                ["ChangeColorTag"] = "🎨 Change Tag Color...",
                ["MoveToGroup"] = "📁 Move to group...",
                ["OpenFolder"] = "Open Data Folder",
                ["Delete"] = "Delete this account",
                ["ConfirmDeleteTitle"] = "Confirm Account Deletion",
                ["ConfirmDeleteMsg"] = "Are you sure you want to delete account '{0}' ({1})?\n(Session data is preserved in Sessions folder for future use).",
                ["ConfirmClearCacheTitle"] = "Confirm Cache Reset",
                ["ConfirmClearCacheMsg"] = "Are you sure you want to clear Cache, Cookies and log out of '{0}' ({1})?",
                ["ClearCacheSuccess"] = "Session data cleared for '{0}'. Please log in again.",
                ["NewMessage"] = "New Message",
                ["OneNewMessage"] = "You have 1 new message.",
                ["MultipleNewMessages"] = "You have {0} new messages ({1} unread).",
                ["EnterAccountNameWarning"] = "Please enter an account name.",
                ["EnterCustomUrlWarning"] = "Please enter a valid URL starting with https://",
                ["RenameTitle"] = "Rename Account",
                ["RenameSubtitle"] = "Enter new name for this account:",
                ["SaveChange"] = "Save Changes"
            }
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
