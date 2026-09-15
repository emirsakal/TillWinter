using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace TillWinter.Tests.Unity
{
    /// <summary>Every launch opens on the title scene; Play loads the farm (build order Menu, Farm).</summary>
    public class SceneTests
    {
        [Test]
        public void BuildOrder_IsMenuThenFarm_AndBothScenesExist()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.GreaterOrEqual(scenes.Length, 2, "run ui-setup.bat");
            StringAssert.EndsWith("/Menu.unity", scenes[0].path);
            StringAssert.EndsWith("/Farm.unity", scenes[1].path);
            Assert.IsTrue(scenes[0].enabled && scenes[1].enabled);
            Assert.IsTrue(File.Exists(scenes[0].path), scenes[0].path);
            Assert.IsTrue(File.Exists(scenes[1].path), scenes[1].path);
            Assert.AreEqual(TillWinter.Unity.SceneNames.Menu, Path.GetFileNameWithoutExtension(scenes[0].path));
            Assert.AreEqual(TillWinter.Unity.SceneNames.Farm, Path.GetFileNameWithoutExtension(scenes[1].path));
        }
    }
}
