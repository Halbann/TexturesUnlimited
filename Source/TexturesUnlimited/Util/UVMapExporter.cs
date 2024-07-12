﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEngine;

namespace KSPShaderTools
{
    /// <summary>
    /// Takes an input Unity Mesh component, and outputs a UV map from the meshes stored UV data
    /// </summary>
    public class UVMapExporter
    {

        public int height = 1024;
        public int width = 1024;
        public int stroke = 1;
        
        public void exportModel(GameObject model, string rootFolder)
        {
            string modelFolderPath = rootFolder + "/" + SanatizeFileName(model.name) + "/";
            MonoBehaviour.print("Output path: " + Path.GetFullPath(modelFolderPath));
            Directory.CreateDirectory(modelFolderPath);
            MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>();
            MeshRenderer render;
            MonoBehaviour.print("Found "+filters.Length+" mesh filters");
            List<string> modelData = new List<string>();
            int len = filters.Length;
            for (int i = 0; i < len; i++)
            {
                render = filters[i].gameObject.GetComponent<MeshRenderer>();
                if (render != null)
                {
                    getModelData(render, modelData);
                }
                if (filters[i].mesh == null)
                {
                    continue;
                }
                string uvMapName = modelFolderPath + SanatizeFileName(filters[i].gameObject.name + ".svg");
                try
                {
                    writeSVG(uvMapName, getMeshUVs(filters[i].mesh));
                }
                catch (Exception e)//bad code is bad...
                {
                    MonoBehaviour.print("ERROR On mesh filter: " + filters[i].gameObject);
                    MonoBehaviour.print(e);
                }
            }
            SkinnedMeshRenderer[] smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            len = smrs.Length;
            for (int i = 0; i < len; i++)
            {
                Mesh mesh = smrs[i].sharedMesh;
                if (mesh == null) { continue; }
                string uvMapName = modelFolderPath + SanatizeFileName(smrs[i].gameObject.name + ".svg");
                try
                {
                    getModelData(smrs[i], modelData);
                    writeSVG(uvMapName, getMeshUVs(mesh));
                }
                catch (Exception e)//bad code is bad...
                {
                    MonoBehaviour.print("ERROR On skinned mesh: " + smrs[i].gameObject);
                    MonoBehaviour.print(e);
                }
            }
            System.IO.File.WriteAllLines(modelFolderPath + "ModelData.txt", modelData);
        }

        /// <summary>
        /// Writes text based model data into the list of data lines.
        /// </summary>
        /// <param name="r"></param>
        /// <param name="data"></param>
        private void getModelData(MeshRenderer r, List<string> data)
        {
            data.Add("-------------------------------------------");
            data.Add("Mesh Name Model: " + r.gameObject.name);
            data.Add("Render Type: MeshRenderer");
            Material[] mats = r.sharedMaterials;
            if (mats != null)
            {
                int len = mats.Length;
                for (int i = 0; i < len; i++)
                {
                    data.Add("++++++++++++++++++");
                    data.Add("Material #: " + i);
                    data.Add(Debug.getMaterialPropertiesDebug(mats[i]));
                    data.Add("++++++++++++++++++");
                }
            }
            data.Add("-------------------------------------------");
        }

        /// <summary>
        /// Writes text based model data into the list of data lines.
        /// </summary>
        /// <param name="r"></param>
        /// <param name="data"></param>
        private void getModelData(SkinnedMeshRenderer r, List<string> data)
        {
            data.Add("-------------------------------------------");
            data.Add("Mesh Name: " + r.gameObject.name);
            data.Add("Render Type: SkinnedMeshRenderer");
            Material[] mats = r.sharedMaterials;
            if (mats != null)
            {
                int len = mats.Length;
                for (int i = 0; i < len; i++)
                {
                    data.Add("++++++++++++++++++");
                    data.Add("Material #: " + i);
                    data.Add(Debug.getMaterialPropertiesDebug(mats[i]));
                    data.Add("++++++++++++++++++");
                }
            }
            data.Add("-------------------------------------------");
        }

        public UVLine[] getMeshUVs(Mesh mesh)
        {
            if (mesh == null) { return new UVLine[0]; }
            List<UVLine> lines = new List<UVLine>();

            int[] triangles = mesh.triangles;
            Vector2[] uvs = mesh.uv;

            int ind1, ind2, ind3;
            Vector2 uv1, uv2, uv3;
            for (int i = 0; i < triangles.Length; i+=3)
            {
                ind1 = triangles[i];
                ind2 = triangles[i + 1];
                ind3 = triangles[i + 2];
                uv1 = uvs[ind1];
                uv2 = uvs[ind2];
                uv3 = uvs[ind3];
                lines.Add(new UVLine(uv1, uv2));
                lines.Add(new UVLine(uv2, uv3));
                lines.Add(new UVLine(uv3, uv1));
            }
            return lines.ToArray();
        }

        public void writeSVG(string fileName, UVLine[] lines)
        {
            if (lines == null || lines.Length <= 0) { return; }
            List<string> output = new List<string>();
            string header = "<?xml version = \"1.0\" standalone = \"no\"?>";
            string docTag = "<!DOCTYPE svg PUBLIC \" -//W3C//DTD SVG 1.1//EN\" \"http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd\">";
            string svgTag = "<svg width = \""+width+"\" height = \""+height+"\" viewBox = \"0 0 "+width+" "+height+"\" xmlns = \"http://www.w3.org/2000/svg\" version = \"1.1\">";
            output.Add(header);
            output.Add(docTag);
            output.Add(svgTag);

            int len = lines.Length;
            for (int i = 0; i < len; i++)
            {
                output.Add(lines[i].getSVGOutput());
            }

            string footer = "</svg>";
            output.Add(footer);
            string path = fileName;
            File.WriteAllLines(path, output.ToArray());
        }

        private static string SanatizeFileName(string input)
        {
            string output = input.Replace(":", "-");//colon
            output = output.Replace("<", "-");//less than
            output = output.Replace(">", "-");//greater than
            output = output.Replace("\"", string.Empty);//double quote
            output = output.Replace("|", string.Empty);//pipe
            output = output.Replace("?", string.Empty);//question mark
            //forward slash -- valid as path separator char
            //back slash -- valid as path separator char
            return output;
        }

        public struct UVLine
        {
            Vector2 start;
            Vector2 end;

            public UVLine(Vector2 start, Vector2 end)
            {
                this.start = start;
                this.end = end;
            }

            public string getSVGOutput()
            {
                return "<line " + getVectorString("1", start) + " " + getVectorString("2", end)+ " stroke = \"black\" stroke-width=\"1\"/>";
            }

            private string getVectorString(string label, Vector3 vector)
            {
                return "x" + label + "=\"" + (vector.x*1024) + "\" y" + label + "=\"" + ((1-vector.y)*1024) + "\"";
            }
        }

    }
}
