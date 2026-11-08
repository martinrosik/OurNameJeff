using System.IO;
using NUnit.Framework;
using ResortEmpire.Editor;
using UnityEngine;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// A clone without Git LFS leaves pointer files instead of images. The Editor check must find them,
    /// and must not flag real images or ordinary text files.
    /// </summary>
    public class LfsPointerCheckTests
    {
        private const string Pointer = "version https://git-lfs.github.com/spec/v1\n"
            + "oid sha256:b3953e0be8bde97630eb7e1ee94a34f08cc408895ddd1244a29958ef5da3ea40\nsize 195\n";

        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ResortEmpireLfsTest_" + Path.GetRandomFileName());
            Directory.CreateDirectory(Path.Combine(_root, "Art"));
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, recursive: true);
        }

        [Test]
        public void FindsPointerFilesWithLfsExtensions()
        {
            string pointerPng = Path.Combine(_root, "Art", "Missing.png");
            File.WriteAllText(pointerPng, Pointer);

            Assert.That(LfsPointerCheck.FindPointerFiles(_root), Is.EquivalentTo(new[] { pointerPng }));
        }

        [Test]
        public void IgnoresRealImagesAndOtherFiles()
        {
            var texture = new Texture2D(4, 4);
            File.WriteAllBytes(Path.Combine(_root, "Art", "Real.png"), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            File.WriteAllText(Path.Combine(_root, "Art", "Notes.md"), Pointer);

            Assert.That(LfsPointerCheck.FindPointerFiles(_root), Is.Empty);
        }

        [Test]
        public void ProjectHasNoPointerFiles()
        {
            Assert.That(LfsPointerCheck.FindPointerFiles(Application.dataPath), Is.Empty,
                "Run 'git lfs install' and 'git lfs pull' in the project folder.");
        }
    }
}
