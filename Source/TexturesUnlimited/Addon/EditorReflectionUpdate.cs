﻿using KSPShaderTools.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KSPShaderTools.Addon
{

    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class EditorReflectionUpdate : MonoBehaviour
    {

        private bool fixedRP;
        private bool enableProbe;
        private GameObject probeObject;
        private ReflectionProbe probe;

        public void Awake()
        {
            fixedRP = false;
        }

        public void Start()
        {
            enableProbe = TUGameSettings.CustomEditorReflections;
            if (enableProbe)
            {
                ReflectionProbe v = (ReflectionProbe)GameObject.FindObjectOfType(typeof(ReflectionProbe));
                probeObject = new GameObject("TUEditorReflectionProbe");
                probeObject.transform.position = new Vector3(0, 10, 0);
                probe = probeObject.AddComponent<ReflectionProbe>();
                probe.size = new Vector3(1000, 1000, 1000);
                probe.resolution = TUGameSettings.ReflectionResolution;
                probe.hdr = false;
                probe.cullingMask = (1 << 4) | (1 << 15) | (1 << 17) | (1 << 23) | (1 << 10) | (1 << 9) | (1 << 18);//everything the old reflection probe system captured
                probe.enabled = true;
                probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                probe.nearClipPlane = 0.1f;
                probe.farClipPlane = 2000f;
                probe.boxProjection = false;
                probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
                probe.backgroundColor = Color.black;
                Log.log("Created custom editor reflection probe.");
                Log.log("    Resolution   : " + probe.resolution);
                Log.log("    Culling Mask : " + probe.cullingMask);
                Log.log("    Near Clip    : " + probe.nearClipPlane);
                Log.log("    Far Clip     : " + probe.farClipPlane);
                Log.log("    Probe Size   : " + probe.size);
                Log.log("    Clear Flags  : " + probe.clearFlags);
            }
        }

        private int delay = 5;
        public void Update()
        {
            if (!fixedRP && enableProbe)
            {
                if (delay > 0)
                {
                    delay--;
                    return;
                }
                fixedRP = true;
                Log.log("Rendering environment for custom editor reflection probe.");
                probe.RenderProbe();
            }
        }

    }

}
