using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Core
{
    public static class NativeFileDialogUtility
    {
        public static string OpenTlsFilePanel(string title, string initialDirectory)
        {
#if UNITY_EDITOR && !UNITY_EDITOR_WIN
            return UnityEditor.EditorUtility.OpenFilePanel(title, initialDirectory, "tls");
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            string[] paths = OpenWindowsFilePanel(title, initialDirectory, false);
            return paths != null && paths.Length > 0 ? paths[0] : string.Empty;
#else
            Debug.LogWarning("[NativeFileDialogUtility] 현재 플랫폼에서는 네이티브 파일 선택창을 지원하지 않습니다.");
            return string.Empty;
#endif
        }

        public static string[] OpenTlsFilesPanel(string title, string initialDirectory)
        {
#if UNITY_EDITOR && !UNITY_EDITOR_WIN
            string path = UnityEditor.EditorUtility.OpenFilePanel(title, initialDirectory, "tls");
            return string.IsNullOrWhiteSpace(path) ? Array.Empty<string>() : new[] { path };
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return OpenWindowsFilePanel(title, initialDirectory, true) ?? Array.Empty<string>();
#else
            Debug.LogWarning("[NativeFileDialogUtility] 현재 플랫폼에서는 네이티브 파일 선택창을 지원하지 않습니다.");
            return Array.Empty<string>();
#endif
        }

        public static string OpenFolderPanel(string title, string initialDirectory)
        {
#if UNITY_EDITOR && !UNITY_EDITOR_WIN
            return UnityEditor.EditorUtility.OpenFolderPanel(title, initialDirectory, "");
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return OpenWindowsFolderPanel(title, initialDirectory);
#else
            Debug.LogWarning("[NativeFileDialogUtility] 현재 플랫폼에서는 네이티브 폴더 선택창을 지원하지 않습니다.");
            return string.Empty;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public IntPtr lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct BrowseInfo
        {
            public IntPtr hwndOwner;
            public IntPtr pidlRoot;
            public IntPtr pszDisplayName;
            public string lpszTitle;
            public uint ulFlags;
            public IntPtr lpfn;
            public IntPtr lParam;
            public int iImage;
        }

        [DllImport("Comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetOpenFileName(ref OpenFileName ofn);

        [DllImport("Shell32.dll", EntryPoint = "SHBrowseForFolderW", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHBrowseForFolder(ref BrowseInfo browseInfo);

        [DllImport("Shell32.dll", EntryPoint = "SHGetPathFromIDListW", CharSet = CharSet.Unicode)]
        private static extern bool SHGetPathFromIDList(IntPtr pidl, IntPtr pszPath);

        [DllImport("Ole32.dll")]
        private static extern void CoTaskMemFree(IntPtr pv);

        private const int OFN_READONLY = 0x00000001;
        private const int OFN_OVERWRITEPROMPT = 0x00000002;
        private const int OFN_HIDEREADONLY = 0x00000004;
        private const int OFN_NOCHANGEDIR = 0x00000008;
        private const int OFN_SHOWHELP = 0x00000010;
        private const int OFN_ENABLEHOOK = 0x00000020;
        private const int OFN_ENABLETEMPLATE = 0x00000040;
        private const int OFN_ENABLETEMPLATEHANDLE = 0x00000080;
        private const int OFN_NOVALIDATE = 0x00000100;
        private const int OFN_ALLOWMULTISELECT = 0x00000200;
        private const int OFN_EXTENSIONDIFFERENT = 0x00000400;
        private const int OFN_PATHMUSTEXIST = 0x00000800;
        private const int OFN_FILEMUSTEXIST = 0x00001000;
        private const int OFN_CREATEPROMPT = 0x00002000;
        private const int OFN_SHAREAWARE = 0x00004000;
        private const int OFN_NOREADONLYRETURN = 0x00008000;
        private const int OFN_NOTESTFILECREATE = 0x00010000;
        private const int OFN_NONETWORKBUTTON = 0x00020000;
        private const int OFN_NOLONGNAMES = 0x00040000;
        private const int OFN_EXPLORER = 0x00080000;
        private const int OFN_NODEREFERENCELINKS = 0x00100000;
        private const int OFN_LONGNAMES = 0x00200000;
        private const int OFN_ENABLEINCLUDENOTIFY = 0x00400000;
        private const int OFN_ENABLESIZING = 0x00800000;
        private const int OFN_DONTADDTORECENT = 0x02000000;
        private const int OFN_FORCESHOWHIDDEN = 0x10000000;
        private const uint BIF_RETURNONLYFSDIRS = 0x00000001;
        private const uint BIF_NEWDIALOGSTYLE = 0x00000040;
        private const int MAX_PATH = 260;

        private static string[] OpenWindowsFilePanel(string title, string initialDirectory, bool allowMultiSelect)
        {
            int maxFileChars = allowMultiSelect ? 65536 : 4096;
            IntPtr fileBuffer = IntPtr.Zero;
            IntPtr fileTitleBuffer = IntPtr.Zero;

            try
            {
                fileBuffer = Marshal.AllocHGlobal(maxFileChars * sizeof(char));
                fileTitleBuffer = Marshal.AllocHGlobal(260 * sizeof(char));

                ZeroMemory(fileBuffer, maxFileChars * sizeof(char));
                ZeroMemory(fileTitleBuffer, 260 * sizeof(char));

                OpenFileName ofn = new OpenFileName
                {
                    lStructSize = Marshal.SizeOf(typeof(OpenFileName)),
                    hwndOwner = IntPtr.Zero,
                    hInstance = IntPtr.Zero,
                    lpstrFilter = "The Laser Stage (*.tls)\0*.tls\0All Files (*.*)\0*.*\0\0",
                    lpstrCustomFilter = IntPtr.Zero,
                    nMaxCustFilter = 0,
                    nFilterIndex = 1,
                    lpstrFile = fileBuffer,
                    nMaxFile = maxFileChars,
                    lpstrFileTitle = fileTitleBuffer,
                    nMaxFileTitle = 260,
                    lpstrInitialDir = Directory.Exists(initialDirectory) ? initialDirectory : string.Empty,
                    lpstrTitle = title,
                    Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR,
                    lpstrDefExt = "tls",
                    lCustData = IntPtr.Zero,
                    lpfnHook = IntPtr.Zero,
                    lpTemplateName = null,
                    pvReserved = IntPtr.Zero,
                    dwReserved = 0,
                    FlagsEx = 0
                };

                if (allowMultiSelect)
                    ofn.Flags |= OFN_ALLOWMULTISELECT;

                bool result = GetOpenFileName(ref ofn);
                if (!result)
                    return Array.Empty<string>();

                string buffer = Marshal.PtrToStringUni(fileBuffer, maxFileChars);
                return ParseWindowsFileBuffer(buffer);
            }
            finally
            {
                if (fileBuffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(fileBuffer);

                if (fileTitleBuffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(fileTitleBuffer);
            }
        }

        private static string OpenWindowsFolderPanel(string title, string initialDirectory)
        {
            IntPtr displayNameBuffer = IntPtr.Zero;
            IntPtr pathBuffer = IntPtr.Zero;
            IntPtr pidl = IntPtr.Zero;

            try
            {
                displayNameBuffer = Marshal.AllocHGlobal(MAX_PATH * sizeof(char));
                pathBuffer = Marshal.AllocHGlobal(MAX_PATH * sizeof(char));
                ZeroMemory(displayNameBuffer, MAX_PATH * sizeof(char));
                ZeroMemory(pathBuffer, MAX_PATH * sizeof(char));

                BrowseInfo browseInfo = new BrowseInfo
                {
                    hwndOwner = IntPtr.Zero,
                    pidlRoot = IntPtr.Zero,
                    pszDisplayName = displayNameBuffer,
                    lpszTitle = string.IsNullOrWhiteSpace(title) ? "폴더 선택" : title,
                    ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE,
                    lpfn = IntPtr.Zero,
                    lParam = IntPtr.Zero,
                    iImage = 0
                };

                pidl = SHBrowseForFolder(ref browseInfo);
                if (pidl == IntPtr.Zero)
                    return string.Empty;

                if (!SHGetPathFromIDList(pidl, pathBuffer))
                    return string.Empty;

                string path = Marshal.PtrToStringUni(pathBuffer);
                if (!string.IsNullOrWhiteSpace(path))
                    return path;

                return Directory.Exists(initialDirectory) ? initialDirectory : string.Empty;
            }
            finally
            {
                if (pidl != IntPtr.Zero)
                    CoTaskMemFree(pidl);

                if (displayNameBuffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(displayNameBuffer);

                if (pathBuffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(pathBuffer);
            }
        }

        private static void ZeroMemory(IntPtr pointer, int byteCount)
        {
            byte[] zero = new byte[Mathf.Min(byteCount, 4096)];
            int remaining = byteCount;
            int offset = 0;

            while (remaining > 0)
            {
                int count = Mathf.Min(zero.Length, remaining);
                Marshal.Copy(zero, 0, pointer + offset, count);
                remaining -= count;
                offset += count;
            }
        }

        private static string[] ParseWindowsFileBuffer(string buffer)
        {
            if (string.IsNullOrEmpty(buffer))
                return Array.Empty<string>();

            int doubleNullIndex = buffer.IndexOf("\0\0", StringComparison.Ordinal);
            if (doubleNullIndex >= 0)
                buffer = buffer.Substring(0, doubleNullIndex);

            if (string.IsNullOrWhiteSpace(buffer))
                return Array.Empty<string>();

            string[] parts = buffer.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 0)
                return Array.Empty<string>();

            if (parts.Length == 1)
                return new[] { parts[0] };

            string directory = parts[0];
            List<string> paths = new List<string>();

            for (int i = 1; i < parts.Length; i++)
            {
                string fileName = parts[i];
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                paths.Add(Path.Combine(directory, fileName));
            }

            return paths.ToArray();
        }
#endif
    }
}
