using DazPose.Toys.Buttplug;
using UnityEditor;
using UnityEngine;

namespace DazPose.Editor.Toys
{
    internal static class ToyControlBt1TestMenu
    {
        [MenuItem("Tools/DAZ Pose/Toys/Run BT.1 Offline Registry Tests")]
        private static void RunBt1Tests()
        {
            string[] failures = ButtplugTransportSelfTests.Run();
            if (failures.Length == 0)
            {
                Debug.Log("BT.1 offline registry tests passed.");
                return;
            }

            foreach (string failure in failures) Debug.LogError("BT.1: " + failure);
            throw new System.InvalidOperationException("BT.1 offline registry tests failed: " + failures.Length);
        }
    }
}
