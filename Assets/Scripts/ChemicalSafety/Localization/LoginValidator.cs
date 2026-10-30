using System.Text;
using System.Text.RegularExpressions;

namespace ChemicalSafety.Localization
{
    public enum LoginError
    {
        None,
        NameEmpty,
        NameTooShort,
        NameHasDigit,
        NameBadChar,
        ContactEmpty,
        EmailInvalid,
        PhoneTooShort,
        PhoneTooLong,
        PhoneInvalid
    }

    /// <summary>Pure functions, no Unity types. Used by the login screen and by EditMode tests.</summary>
    public static class LoginValidator
    {
        private static readonly Regex EmailRx = new Regex(
            @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?)*\.[A-Za-z]{2,}$",
            RegexOptions.Compiled);

        public static bool IsDigit(char c) => (c >= '0' && c <= '9') || (c >= '০' && c <= '৯') ||
                                              (c >= '٠' && c <= '٩') || (c >= '۰' && c <= '۹');

        public static LoginError ValidateName(string raw, out string cleaned)
        {
            cleaned = (raw ?? string.Empty).Trim();
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
            if (cleaned.Length == 0)
                return LoginError.NameEmpty;
            foreach (var c in cleaned)
            {
                if (IsDigit(c))
                    return LoginError.NameHasDigit;
                if (!(char.IsLetter(c) || c == ' ' || c == '.' || c == '\'' || c == '-' || (c >= 'ঀ' && c <= '৿')))
                    return LoginError.NameBadChar;
            }

            return cleaned.Length < 2 ? LoginError.NameTooShort : LoginError.None;
        }

        /// <summary>Allowed while typing: everything except digits (the keyboard simply ignores them).</summary>
        public static bool IsNameCharAllowed(char c) =>
            !IsDigit(c) && (char.IsLetter(c) || c == ' ' || c == '.' || c == '\'' || c == '-');

        /// <summary>Bangla / Arabic digits become 0-9; spaces, dashes and brackets are dropped.</summary>
        public static string NormalizeDigits(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s)
            {
                if (c >= '০' && c <= '৯') sb.Append((char)('0' + (c - '০')));
                else if (c >= '٠' && c <= '٩') sb.Append((char)('0' + (c - '٠')));
                else if (c >= '۰' && c <= '۹') sb.Append((char)('0' + (c - '۰')));
                else sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Contact is an e-mail (contains '@') or a Bangladeshi mobile number: optional +88 / 88, then exactly 11 digits
        /// (01 + operator digit 3-9 + 8 digits). digitCount reports how many digits were found without the prefix.
        /// </summary>
        public static LoginError ValidateContact(string raw, out string normalized, out bool isEmail, out int digitCount)
        {
            normalized = (raw ?? string.Empty).Trim();
            isEmail = false;
            digitCount = 0;
            if (normalized.Length == 0)
                return LoginError.ContactEmpty;

            if (normalized.IndexOf('@') >= 0)
            {
                isEmail = true;
                return EmailRx.IsMatch(normalized) ? LoginError.None : LoginError.EmailInvalid;
            }

            var s = NormalizeDigits(normalized);
            s = Regex.Replace(s, @"[\s\-\(\)\.]", string.Empty);
            if (s.StartsWith("+88"))
                s = s.Substring(3);
            else if (s.StartsWith("88") && s.Length > 11)
                s = s.Substring(2);

            foreach (var c in s)
            {
                if (c < '0' || c > '9')
                    return LoginError.PhoneInvalid;
            }

            digitCount = s.Length;
            normalized = s;
            if (s.Length < 11)
                return LoginError.PhoneTooShort;
            if (s.Length > 11)
                return LoginError.PhoneTooLong;
            return Regex.IsMatch(s, @"^01[3-9][0-9]{8}$") ? LoginError.None : LoginError.PhoneInvalid;
        }
    }
}
