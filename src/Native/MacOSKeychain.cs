using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace SourceGit.Native
{
    /// <summary>
    /// Stores secrets (such as AI service API keys) in the macOS login Keychain as
    /// generic-password items, so they never need to be written to preference.json.
    /// </summary>
    [SupportedOSPlatform("macOS")]
    internal static class MacOSKeychain
    {
        private const string Security = "/System/Library/Frameworks/Security.framework/Security";
        private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        private const uint EncodingUTF8 = 0x08000100;

        private const int StatusSuccess = 0;
        private const int StatusDuplicateItem = -25299;

        /// <summary>
        /// Reads the secret for <paramref name="account"/>, or null if none is stored.
        /// </summary>
        public static string Read(string service, string account)
        {
            var query = IntPtr.Zero;
            try
            {
                query = CreateQuery(service, account, withReturnData: true);
                var status = SecItemCopyMatching(query, out var result);
                if (status != StatusSuccess || result == IntPtr.Zero)
                    return null;

                try
                {
                    var length = (int)CFDataGetLength(result);
                    if (length <= 0)
                        return string.Empty;

                    var ptr = CFDataGetBytePtr(result);
                    var bytes = new byte[length];
                    Marshal.Copy(ptr, bytes, 0, length);
                    return Encoding.UTF8.GetString(bytes);
                }
                finally
                {
                    Release(result);
                }
            }
            finally
            {
                Release(query);
            }
        }

        /// <summary>
        /// Stores (adds or updates) the secret for <paramref name="account"/>.
        /// </summary>
        public static bool Write(string service, string account, string secret)
        {
            var secretBytes = Encoding.UTF8.GetBytes(secret);

            var existsQuery = IntPtr.Zero;
            try
            {
                existsQuery = CreateQuery(service, account, withReturnData: false);
                var exists = SecItemCopyMatching(existsQuery, out _) == StatusSuccess;
                if (exists)
                    return Update(existsQuery, secretBytes);

                var addService = CreateString(service);
                var addAccount = CreateString(account);
                var addData = CFDataCreate(IntPtr.Zero, secretBytes, secretBytes.Length);
                var attributes = CreateDictionary(
                    [_kSecClass.Value, _kSecAttrService.Value, _kSecAttrAccount.Value, _kSecValueData.Value],
                    [_kSecClassGenericPassword.Value, addService, addAccount, addData]);
                try
                {
                    var status = SecItemAdd(attributes, IntPtr.Zero);

                    // A concurrent writer may have created the item between the check and
                    // the add; fall back to updating it instead of failing.
                    if (status == StatusDuplicateItem)
                        return Update(existsQuery, secretBytes);

                    return status == StatusSuccess;
                }
                finally
                {
                    Release(attributes);
                    Release(addData);
                    Release(addAccount);
                    Release(addService);
                }
            }
            finally
            {
                Release(existsQuery);
            }
        }

        private static bool Update(IntPtr query, byte[] secretBytes)
        {
            var data = CFDataCreate(IntPtr.Zero, secretBytes, secretBytes.Length);
            var update = CreateDictionary([_kSecValueData.Value], [data]);
            try
            {
                return SecItemUpdate(query, update) == StatusSuccess;
            }
            finally
            {
                Release(update);
                Release(data);
            }
        }

        /// <summary>
        /// Removes the secret for <paramref name="account"/> if present.
        /// </summary>
        public static void Delete(string service, string account)
        {
            var query = CreateQuery(service, account, withReturnData: false);
            try
            {
                // Best effort: a failure here just leaves a harmless stale item.
                SecItemDelete(query);
            }
            finally
            {
                Release(query);
            }
        }

        private static IntPtr CreateQuery(string service, string account, bool withReturnData)
        {
            var serviceRef = CreateString(service);
            var accountRef = CreateString(account);
            try
            {
                if (withReturnData)
                {
                    return CreateDictionary(
                        [_kSecClass.Value, _kSecAttrService.Value, _kSecAttrAccount.Value, _kSecReturnData.Value, _kSecMatchLimit.Value],
                        [_kSecClassGenericPassword.Value, serviceRef, accountRef, _kCFBooleanTrue.Value, _kSecMatchLimitOne.Value]);
                }

                return CreateDictionary(
                    [_kSecClass.Value, _kSecAttrService.Value, _kSecAttrAccount.Value],
                    [_kSecClassGenericPassword.Value, serviceRef, accountRef]);
            }
            finally
            {
                Release(serviceRef);
                Release(accountRef);
            }
        }

        private static IntPtr CreateString(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value + '\0');
            return CFStringCreateWithCString(IntPtr.Zero, bytes, EncodingUTF8);
        }

        private static IntPtr CreateDictionary(IntPtr[] keys, IntPtr[] values)
        {
            // CFDictionaryCreate retains each key/value via its callbacks; a NULL entry
            // would crash in CFRetain. Bail if any component handle failed to allocate.
            foreach (var key in keys)
                if (key == IntPtr.Zero)
                    return IntPtr.Zero;

            foreach (var value in values)
                if (value == IntPtr.Zero)
                    return IntPtr.Zero;

            return CFDictionaryCreate(
                IntPtr.Zero,
                keys,
                values,
                keys.Length,
                _kCFTypeDictionaryKeyCallBacks.Value,
                _kCFTypeDictionaryValueCallBacks.Value);
        }

        private static void Release(IntPtr handle)
        {
            if (handle != IntPtr.Zero)
                CFRelease(handle);
        }

        // Exported CFStringRef / struct constants are read once from the frameworks.
        private static readonly Lazy<IntPtr> _securityHandle = new(() => NativeLibrary.Load(Security));
        private static readonly Lazy<IntPtr> _coreFoundationHandle = new(() => NativeLibrary.Load(CoreFoundation));

        private static Lazy<IntPtr> SecurityConstant(string name) =>
            new(() => Marshal.ReadIntPtr(NativeLibrary.GetExport(_securityHandle.Value, name)));

        private static Lazy<IntPtr> CoreFoundationConstant(string name) =>
            new(() => Marshal.ReadIntPtr(NativeLibrary.GetExport(_coreFoundationHandle.Value, name)));

        private static Lazy<IntPtr> CoreFoundationStruct(string name) =>
            new(() => NativeLibrary.GetExport(_coreFoundationHandle.Value, name));

        private static readonly Lazy<IntPtr> _kSecClass = SecurityConstant("kSecClass");
        private static readonly Lazy<IntPtr> _kSecClassGenericPassword = SecurityConstant("kSecClassGenericPassword");
        private static readonly Lazy<IntPtr> _kSecAttrService = SecurityConstant("kSecAttrService");
        private static readonly Lazy<IntPtr> _kSecAttrAccount = SecurityConstant("kSecAttrAccount");
        private static readonly Lazy<IntPtr> _kSecValueData = SecurityConstant("kSecValueData");
        private static readonly Lazy<IntPtr> _kSecReturnData = SecurityConstant("kSecReturnData");
        private static readonly Lazy<IntPtr> _kSecMatchLimit = SecurityConstant("kSecMatchLimit");
        private static readonly Lazy<IntPtr> _kSecMatchLimitOne = SecurityConstant("kSecMatchLimitOne");

        private static readonly Lazy<IntPtr> _kCFBooleanTrue = CoreFoundationConstant("kCFBooleanTrue");
        private static readonly Lazy<IntPtr> _kCFTypeDictionaryKeyCallBacks = CoreFoundationStruct("kCFTypeDictionaryKeyCallBacks");
        private static readonly Lazy<IntPtr> _kCFTypeDictionaryValueCallBacks = CoreFoundationStruct("kCFTypeDictionaryValueCallBacks");

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, byte[] cStr, uint encoding);

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFDataCreate(IntPtr alloc, byte[] bytes, long length);

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFDataGetBytePtr(IntPtr data);

        [DllImport(CoreFoundation)]
        private static extern long CFDataGetLength(IntPtr data);

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFDictionaryCreate(IntPtr alloc, IntPtr[] keys, IntPtr[] values, long numValues, IntPtr keyCallBacks, IntPtr valueCallBacks);

        [DllImport(CoreFoundation)]
        private static extern void CFRelease(IntPtr cf);

        [DllImport(Security)]
        private static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);

        [DllImport(Security)]
        private static extern int SecItemAdd(IntPtr attributes, IntPtr result);

        [DllImport(Security)]
        private static extern int SecItemUpdate(IntPtr query, IntPtr attributesToUpdate);

        [DllImport(Security)]
        private static extern int SecItemDelete(IntPtr query);
    }
}
