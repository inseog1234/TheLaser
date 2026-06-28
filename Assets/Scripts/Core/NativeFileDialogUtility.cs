using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Core
{
    public static class NativeFileDialogUtility
    {
        public static string OpenTlsFilePanel(string title, string initialDirectory)
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.OpenFilePanel(title, initialDirectory, "tls");
#elif UNITY_STANDALONE_WIN
            return OpenWindowsFilePanel(title, initialDirectory);
#else
            Debug.LogWarning("[NativeFileDialogUtility] 현재 플랫폼에서는 네이티브 파일 선택창을 지원하지 않습니다.");
            return string.Empty;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class OpenFileName
        {
            public int structSize = 0;
            public IntPtr dlgOwner = IntPtr.Zero;
            public IntPtr instance = IntPtr.Zero;
            public string filter = null;
            public string customFilter = null;
            public int maxCustFilter = 0;
            public int filterIndex = 0;
            public StringBuilder file = null;
            public int maxFile = 0;
            public string fileTitle = null;
            public int maxFileTitle = 0;
            public string initialDir = null;
            public string title = null;
            public int flags = 0;
            public short fileOffset = 0;
            public short fileExtension = 0;
            public string defExt = null;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public string templateName = null;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt = 0;
            public int flagsEx = 0;
        }

        [DllImport("Comdlg32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        private static string OpenWindowsFilePanel(string title, string initialDirectory)
        {
            OpenFileName ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(ofn);
            ofn.filter = "The Laser Stage (*.tls)\0*.tls\0All Files (*.*)\0*.*\0";
            ofn.file = new StringBuilder(1024);
            ofn.maxFile = ofn.file.Capacity;
            ofn.fileTitle = new string(new char[256]);
            ofn.maxFileTitle = ofn.fileTitle.Length;
            ofn.initialDir = initialDirectory;
            ofn.title = title;
            ofn.defExt = "tls";
            ofn.flags = 0x00080000 | 0x00001000 | 0x00000800;

            bool result = GetOpenFileName(ofn);
            return result ? ofn.file.ToString() : string.Empty;
        }
#endif
    }
}
