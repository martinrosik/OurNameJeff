using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ResortEmpire.Editor
{
    /// <summary>
    /// Images, fonts and audio are stored in Git LFS. A clone or pull without Git LFS leaves small text
    /// "pointer" files in their place. Unity can't import those, so every sprite shows up empty with no
    /// obvious cause. This check finds pointer files once per Editor session and says how to fix it.
    /// </summary>
    [InitializeOnLoad]
    public static class LfsPointerCheck
    {
        private const string PointerHeader = "version https://git-lfs.github.com/spec/v1";
        private const string SessionKey = "ResortEmpire.LfsPointerCheck.Done";

        // Real pointer files are about 130 bytes. Anything bigger is a real asset and is never read.
        private const int MaxPointerSize = 1024;

        // The binary types .gitattributes sends to Git LFS.
        private static readonly HashSet<string> LfsExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".psd", ".psb", ".aseprite", ".ase", ".tga", ".tif", ".tiff",
            ".exr", ".hdr", ".bmp", ".webp", ".wav", ".mp3", ".ogg", ".aif", ".aiff", ".flac", ".ttf", ".otf",
            ".mp4", ".mov", ".webm", ".fbx", ".blend", ".zip", ".pdf",
        };

        static LfsPointerCheck()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += () => Run(showDialog: true);
        }

        [MenuItem("Resort Empire/Check Git LFS Files", priority = 100)]
        private static void RunFromMenu()
        {
            if (Run(showDialog: true) == 0)
            {
                EditorUtility.DisplayDialog("Git LFS files", "All Git LFS files are present.", "OK");
            }
        }

        /// <summary>Logs an error (and optionally shows a dialog) for each pointer file. Returns how many it found.</summary>
        private static int Run(bool showDialog)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            List<string> pointers = FindPointerFiles(Application.dataPath)
                .Select(path => Path.GetRelativePath(projectRoot, path).Replace('\\', '/'))
                .ToList();

            if (pointers.Count == 0)
            {
                return 0;
            }

            const string fix = "Fix: in the project folder run\n\n    git lfs install\n    git lfs pull\n\n"
                + "then come back to Unity. It reimports the files by itself.";
            string files = pointers.Count == 1 ? "1 file in Assets is a Git LFS pointer" : $"{pointers.Count} files in Assets are Git LFS pointers";
            string summary = $"{files}, not the real file, so sprites, icons and other art show up empty. "
                + "The project was cloned or pulled without Git LFS.";

            Debug.LogError($"[Resort Empire] {summary}\n{fix}\n\nMissing:\n{string.Join("\n", pointers)}");

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Art files are missing (Git LFS)", $"{summary}\n\n{fix}", "OK");
            }

            return pointers.Count;
        }

        /// <summary>All files under <paramref name="root"/> with a Git LFS extension that hold a pointer.</summary>
        public static List<string> FindPointerFiles(string root)
        {
            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(path => LfsExtensions.Contains(Path.GetExtension(path)))
                .Where(IsPointerFile)
                .ToList();
        }

        private static bool IsPointerFile(string path)
        {
            var info = new FileInfo(path);
            if (info.Length > MaxPointerSize)
            {
                return false;
            }

            byte[] header = Encoding.ASCII.GetBytes(PointerHeader);
            byte[] start = new byte[header.Length];
            using (FileStream stream = info.OpenRead())
            {
                int read = stream.Read(start, 0, start.Length);
                return read == header.Length && start.SequenceEqual(header);
            }
        }
    }
}
