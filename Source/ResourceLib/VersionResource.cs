using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.IO;

namespace Vestris.ResourceLib
{
    /// <summary>
    /// VS_VERSIONINFO
    /// This structure depicts the organization of data in a file-version resource. It is the root structure 
    /// that contains all other file-version information structures.
    /// http://msdn.microsoft.com/en-us/library/aa914916.aspx
    /// </summary>
    public class VersionResource : Resource
    {
        ResourceTable _header = new ResourceTable("VS_VERSION_INFO");
        Kernel32.VS_FIXEDFILEINFO _fixedfileinfo = Kernel32.VS_FIXEDFILEINFO.GetWindowsDefault();
        private Dictionary<string, ResourceTable> _resources = new Dictionary<string, ResourceTable>();

        /// <summary>
        /// The resource header.
        /// </summary>
        public ResourceTable Header
        {
            get
            {
                return _header;
            }
        }

        /// <summary>
        /// A dictionary of resource tables.
        /// </summary>
        public Dictionary<string, ResourceTable> Resources
        {
            get
            {
                return _resources;
            }
        }

        /// <summary>
        /// An existing version resource.
        /// </summary>
        /// <param name="hModule">Module handle.</param>
        /// <param name="hResource">Resource ID.</param>
        /// <param name="type">Resource type.</param>
        /// <param name="name">Resource name.</param>
        /// <param name="wIDLanguage">Language ID.</param>
        /// <param name="size">Resource size.</param>
        public VersionResource(IntPtr hModule, IntPtr hResource, IntPtr type, IntPtr name, UInt16 wIDLanguage, int size)
            : base(hModule, hResource, type, name, wIDLanguage, size)
        {
            IntPtr lpRes = Kernel32.LockResource(hResource);

            if (lpRes == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            Read(hModule, lpRes);
        }

        /// <summary>
        /// A new language-netural version resource.
        /// </summary>
        public VersionResource()
            : base(IntPtr.Zero, IntPtr.Zero, new IntPtr((uint) Kernel32.ResourceTypes.RT_VERSION), new IntPtr(1), ResourceUtil.USENGLISHLANGID, 0)
        {
            _header.Header = new Kernel32.RESOURCE_HEADER((UInt16) Marshal.SizeOf(_fixedfileinfo));
        }

        /// <summary>
        /// Load language-neutral version resource.
        /// </summary>
        /// <param name="filename">Source file.</param>
        public void LoadFrom(string filename)
        {
            LoadFrom(filename, ResourceUtil.NEUTRALLANGID);
        }

        /// <summary>
        /// Load version resource data.
        /// </summary>
        /// <param name="filename">Source file.</param>
        /// <param name="lang">Resource language.</param>
        public void LoadFrom(string filename, UInt16 lang)
        {
            base.LoadFrom(filename, new IntPtr(1), new IntPtr((uint) Kernel32.ResourceTypes.RT_VERSION), lang);
        }

        /// <summary>
        /// Load language-neutral version resource data.
        /// </summary>
        /// <param name="filename">Source filename.</param>
        /// <returns>Resource data.</returns>
        public static byte[] LoadBytesFrom(string filename)
        {
            return LoadBytesFrom(filename, ResourceUtil.NEUTRALLANGID);
        }

        /// <summary>
        /// Load version resource data.
        /// </summary>
        /// <param name="filename">Source filename.</param>
        /// <param name="lang">Resource language.</param>
        /// <returns>Resource data.</returns>
        public static byte[] LoadBytesFrom(string filename, UInt16 lang)
        {
            return Resource.LoadBytesFrom(filename, new IntPtr(1), new IntPtr((uint) Kernel32.ResourceTypes.RT_VERSION), lang);
        }

        /// <summary>
        /// Read a version resource from a previously loaded module.
        /// </summary>
        /// <param name="hModule">Module handle.</param>
        /// <param name="lpRes">Pointer to the beginning of the resource.</param>
        /// <returns>Pointer to the end of the resource.</returns>
        internal override IntPtr Read(IntPtr hModule, IntPtr lpRes)
        {
            _resources.Clear();

            IntPtr pFixedFileInfo = _header.Read(lpRes);

            _fixedfileinfo = (Kernel32.VS_FIXEDFILEINFO)Marshal.PtrToStructure(
                pFixedFileInfo, typeof(Kernel32.VS_FIXEDFILEINFO));

            IntPtr pChild = ResourceUtil.Align(pFixedFileInfo.ToInt32() + _header.Header.wValueLength);

            while (pChild.ToInt32() < (lpRes.ToInt32() + _header.Header.wLength))
            {
                ResourceTable rc = new ResourceTable(pChild);
                switch (rc.Key)
                {
                    case "StringFileInfo":
                        StringFileInfo sr = new StringFileInfo(pChild);
                        _language = sr.Default.LanguageID;
                        rc = sr;
                        break;
                    default:
                        rc = new VarFileInfo(pChild);
                        break;
                }

                _resources.Add(rc.Key, rc);
                pChild = ResourceUtil.Align(pChild.ToInt32() + rc.Header.wLength);
            }

            return new IntPtr(lpRes.ToInt32() + _header.Header.wLength);
        }

        /// <summary>
        /// String representation of the file version.
        /// </summary>
        public string FileVersion
        {
            get
            {
                return string.Format("{0}.{1}.{2}.{3}",
                    (_fixedfileinfo.dwFileVersionMS & 0xffff0000) >> 16,
                    _fixedfileinfo.dwFileVersionMS & 0x0000ffff,
                    (_fixedfileinfo.dwFileVersionLS & 0xffff0000) >> 16,
                    _fixedfileinfo.dwFileVersionLS & 0x0000ffff);
            }
            set
            {
                UInt32 major = 0, minor = 0, build = 0, release = 0;
                string[] version_s = value.Split(".".ToCharArray(), 4);
                if (version_s.Length >= 1) major = UInt32.Parse(version_s[0]);
                if (version_s.Length >= 2) minor = UInt32.Parse(version_s[1]);
                if (version_s.Length >= 3) build = UInt32.Parse(version_s[2]);
                if (version_s.Length >= 4) release = UInt32.Parse(version_s[3]);
                _fixedfileinfo.dwFileVersionMS = (major << 16) + minor;
                _fixedfileinfo.dwFileVersionLS = (build << 16) + release;
            }
        }

        /// <summary>
        /// String representation of the protect version.
        /// </summary>
        public string ProductVersion
        {
            get
            {
                return string.Format("{0}.{1}.{2}.{3}",
                    (_fixedfileinfo.dwProductVersionMS & 0xffff0000) >> 16,
                    _fixedfileinfo.dwProductVersionMS & 0x0000ffff,
                    (_fixedfileinfo.dwProductVersionLS & 0xffff0000) >> 16,
                    _fixedfileinfo.dwProductVersionLS & 0x0000ffff);
            }
            set
            {
                UInt32 major = 0, minor = 0, build = 0, release = 0;
                string[] version_s = value.Split(".".ToCharArray(), 4);
                if (version_s.Length >= 1) major = UInt32.Parse(version_s[0]);
                if (version_s.Length >= 2) minor = UInt32.Parse(version_s[1]);
                if (version_s.Length >= 3) build = UInt32.Parse(version_s[2]);
                if (version_s.Length >= 4) release = UInt32.Parse(version_s[3]);
                _fixedfileinfo.dwProductVersionMS = (major << 16) + minor;
                _fixedfileinfo.dwProductVersionLS = (build << 16) + release;
            }
        }

        /// <summary>
        /// Write this version resource to a binary stream.
        /// </summary>
        /// <param name="w">Binary stream.</param>
        internal override void Write(BinaryWriter w)
        {
            long headerPos = w.BaseStream.Position;
            _header.Write(w);
            
            w.Write(ResourceUtil.GetBytes<Kernel32.VS_FIXEDFILEINFO>(_fixedfileinfo));
            ResourceUtil.PadToDWORD(w);

            Dictionary<string, ResourceTable>.Enumerator resourceEnum = _resources.GetEnumerator();
            while (resourceEnum.MoveNext())
            {
                resourceEnum.Current.Value.Write(w);
            }

            ResourceUtil.WriteAt(w, w.BaseStream.Position - headerPos, headerPos);
        }

        /// <summary>
        /// Save version resource bytes to a file.
        /// </summary>
        /// <param name="filename">Target filename.</param>
        /// <param name="data">Raw version resource data.</param>
        public static void SaveTo(string filename, byte[] data)
        {
            Resource.SaveTo(filename, new IntPtr(1), new IntPtr((uint)Kernel32.ResourceTypes.RT_VERSION), 
                ResourceUtil.USENGLISHLANGID, data);
        }

        /// <summary>
        /// Save this version resource to a file.
        /// </summary>
        /// <param name="filename">Target filename.</param>
        public void SaveTo(string filename)
        {
            base.SaveTo(filename, new IntPtr(1), new IntPtr((uint) Kernel32.ResourceTypes.RT_VERSION),
                ResourceUtil.USENGLISHLANGID);
        }

        /// <summary>
        /// Returns an entry within this resource table.
        /// </summary>
        /// <param name="key">Entry key.</param>
        /// <returns>A resource table.</returns>
        public ResourceTable this[string key]
        {
            get
            {
                return Resources[key];
            }
            set
            {
                Resources[key] = value;
            }
        }
    }
}
