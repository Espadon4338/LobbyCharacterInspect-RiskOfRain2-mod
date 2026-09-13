using BepInEx;
using HarmonyLib;
using System;
using UnityEngine;

namespace ExamplePlugin
{
    [BepInPlugin("com.espadon.LobbyCharacterInspect", "Lobby Character Inspect", "1.0.0")]
    public class LobbyRotatorPlugin : BaseUnityPlugin
    {
        private static float rotationSpeed = 350.0f; // Multiplier adjusted for frame dragging
        private static bool isDragging = false;
        private static Vector3 lastMousePosition;

        private static float defaultMenuAngleOffset = -35.0f;
        private static float totalRotationalOffset = 0f; // Global tracking angle accumulator

        private static float currentVelocity = 0f;
        private static float frictionCoefficient = 0.95f; // Speed decay multiplier per frame (closer to 1.0 = spins longer)
        private static float minVelocityCutoff = 0.05f;   // Stopping threshold to save CPU overhead

        public void Awake()
        {
            totalRotationalOffset = defaultMenuAngleOffset;

            try
            {
                var harmony = new Harmony("com.espadon.LobbyCharacterInspect");

                Type slotControllerType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var types = asm.GetTypes();
                    slotControllerType = Array.Find(types, t => t.Name == "SurvivorManikinSlotController" || t.Name == "SurvivorMannequinSlotController");
                    if (slotControllerType != null) break;
                }

                if (slotControllerType != null)
                {
                    var originalLateUpdate = slotControllerType.GetMethod("LateUpdate", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                             ?? slotControllerType.GetMethod("Update", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    var postfixLateUpdate = typeof(LobbyRotatorPlugin).GetMethod(nameof(PedestalLateUpdatePostfix), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                    if (originalLateUpdate != null && postfixLateUpdate != null)
                    {
                        harmony.Patch(originalLateUpdate, postfix: new HarmonyMethod(postfixLateUpdate));
                        Logger.LogInfo("Lobby Rotator Engine running with inertia physics!");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Lobby Rotator initialization block failed: {ex.Message}");
            }
        }

        public void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                isDragging = true;
                lastMousePosition = Input.mousePosition;
                currentVelocity = 0f; // Wipe current physics momentum on click down
            }

            if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }

            if (isDragging && Input.GetMouseButton(0))
            {
                Vector3 currentMousePosition = Input.mousePosition;
                Vector3 delta = currentMousePosition - lastMousePosition;

                // Calculate ongoing cursor velocity tracking data
                currentVelocity = -delta.x * rotationSpeed * Time.deltaTime;

                totalRotationalOffset += currentVelocity;
                lastMousePosition = currentMousePosition;
            }
            else
            {
                if (Mathf.Abs(currentVelocity) > minVelocityCutoff)
                {
                    // Decay velocity using friction scaling calculations across execution loops
                    currentVelocity *= frictionCoefficient;
                    totalRotationalOffset += currentVelocity;
                }
                else
                {
                    currentVelocity = 0f; // Hard stop processing once cutoff threshold is cleared
                }
            }

            // Right-click instantly snaps all pedestal orientation offsets back to 0
            if (Input.GetMouseButtonDown(1))
            {
                totalRotationalOffset = defaultMenuAngleOffset;
                currentVelocity = 0f;
            }
        }

        private static void PedestalLateUpdatePostfix(MonoBehaviour __instance)
        {
            try
            {
                if (__instance != null)
                {
                    __instance.transform.localRotation = Quaternion.Euler(0f, totalRotationalOffset, 0f);
                }
            }
            catch { }
        }
    }
}
