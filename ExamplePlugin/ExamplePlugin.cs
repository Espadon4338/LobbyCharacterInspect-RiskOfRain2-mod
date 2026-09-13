using BepInEx;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace ExamplePlugin
{
    [BepInPlugin("com.espadon.LobbyCharacterInspect", "Lobby Character Inspect", "1.0.0")]
    public class LobbyRotatorPlugin : BaseUnityPlugin
    {
        public void Awake()
        {
            try
            {
                var harmony = new Harmony("com.espadon.LobbyCharacterInspect");

                // Dynamic type assembly scan to find the platform pedestal class structure
                Type slotControllerType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var types = asm.GetTypes();
                    slotControllerType = Array.Find(types, t => t.Name == "SurvivorManikinSlotController" || t.Name == "SurvivorMannequinSlotController");
                    if (slotControllerType != null) break;
                }

                if (slotControllerType != null)
                {
                    // Hook into Update to attach our isolated player-checking controller component
                    MethodInfo originalUpdate = slotControllerType.GetMethod("Update", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    MethodInfo postfixUpdate = typeof(LobbyRotatorPlugin).GetMethod(nameof(PedestalUpdatePostfix), BindingFlags.NonPublic | BindingFlags.Static);

                    if (originalUpdate != null && postfixUpdate != null)
                    {
                        harmony.Patch(originalUpdate, postfix: new HarmonyMethod(postfixUpdate));
                        Logger.LogInfo("Lobby Rotator successfully configured for Local Player Only protection!");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Lobby Rotator initialization block failed: {ex.Message}");
            }
        }

        private static void PedestalUpdatePostfix(MonoBehaviour __instance)
        {
            try
            {
                if (__instance != null && !__instance.GetComponent<OnlyMyCharacterRotator>())
                {
                    // Check if this specific pedestal slot belongs to the local player player controller
                    bool isMine = false;

                    // Method A: Check for native network identity owner flag fields
                    FieldInfo localPlayerField = __instance.GetType().GetField("isLocalPlayer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (localPlayerField != null)
                    {
                        isMine = (bool)localPlayerField.GetValue(__instance);
                    }
                    else
                    {
                        // Method B: Fallback check on network user parameters if field naming scales
                        PropertyInfo readOnlyUser = __instance.GetType().GetProperty("networkUser", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (readOnlyUser != null)
                        {
                            var userObj = readOnlyUser.GetValue(__instance);
                            if (userObj != null)
                            {
                                PropertyInfo localField = userObj.GetType().GetProperty("isLocalPlayer", BindingFlags.Public | BindingFlags.Instance);
                                if (localField != null) isMine = (bool)localField.GetValue(userObj);
                            }
                        }
                        else
                        {
                            // Method C: Default fallback check for singleplayer profile rows or slot 0 layouts
                            FieldInfo slotIndexField = __instance.GetType().GetField("slotIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (slotIndexField != null)
                            {
                                int index = (int)slotIndexField.GetValue(__instance);
                                if (index == 0) isMine = true; // Index 0 is always the primary user asset window
                            }
                        }
                    }

                    // Only inject our drag mechanics onto the platform tracking YOUR selected model asset layer
                    if (isMine)
                    {
                        __instance.gameObject.AddComponent<OnlyMyCharacterRotator>();
                    }
                }
            }
            catch { }
        }
    }

    // --- Isolated Component: Only processes tracking physics if it sits on your active platform pedestal ---
    public class OnlyMyCharacterRotator : MonoBehaviour
    {
        private float rotationSpeed = 350.0f;
        private bool isDragging = false;
        private Vector3 lastMousePosition;

        // Alignment Constants
        private float defaultMenuAngleOffset = -35.0f;
        private float currentRotationalOffset = -35.0f;

        // Inertia Parameters
        private float currentVelocity = 0f;
        private float frictionCoefficient = 0.95f;
        private float minVelocityCutoff = 0.05f;

        public void Start()
        {
            currentRotationalOffset = defaultMenuAngleOffset;
        }

        public void Update()
        {
            // 1. Start tracking dragging state on left-click down anywhere on the screen asset bounds
            if (Input.GetMouseButtonDown(0))
            {
                isDragging = true;
                lastMousePosition = Input.mousePosition;
                currentVelocity = 0f;
            }

            // 2. Clear tracking parameters on click release
            if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }

            // 3. Process mouse tracking mathematics locally
            if (isDragging && Input.GetMouseButton(0))
            {
                Vector3 currentMousePosition = Input.mousePosition;
                Vector3 delta = currentMousePosition - lastMousePosition;

                currentVelocity = -delta.x * rotationSpeed * Time.deltaTime;
                currentRotationalOffset += currentVelocity;
                lastMousePosition = currentMousePosition;
            }
            else
            {
                // Momentum physics process isolated cleanly for your chosen survivor only
                if (Mathf.Abs(currentVelocity) > minVelocityCutoff)
                {
                    currentVelocity *= frictionCoefficient;
                    currentRotationalOffset += currentVelocity;
                }
                else
                {
                    currentVelocity = 0f;
                }
            }

            // Right-click anywhere instantly resets your character back to its clean diagonal presentation stance pose
            if (Input.GetMouseButtonDown(1))
            {
                currentRotationalOffset = defaultMenuAngleOffset;
                currentVelocity = 0f;
            }
        }

        // LateUpdate forces your rotation matrix modification out AFTER skin and UI fixes run their overwrite sweeps
        public void LateUpdate()
        {
            transform.localRotation = Quaternion.Euler(0f, currentRotationalOffset, 0f);
        }
    }
}
