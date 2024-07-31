using System;
using System.Collections.Generic;
using UnityEngine;

namespace KSPShaderTools
{

    // Resonsible for tracking list of texture switch options, 
    // managing of actual switching of textures,
    // and restoring persistent option on reload.
    // may be controlled through external module (e.g resource or mesh-switch) through the two methods restoreDefaultTexture() and enableTextureSet(String setName)
    public class KSPTextureSwitch : PartModule, IRecolorable
    {

        /// <summary>
        /// The root transform name that this texture-switch module should operate on.  Omit/leave blank to use the root 'model' transform from the part.
        /// This is generally only needed if the texture sets themselves do not define include/exclusion specifications.
        /// </summary>
        [KSPField]
        public string transformName = string.Empty;

        /// <summary>
        /// A CSV list of root transform names to traverse when looking for model-related transforms.  Defaults to the stock 'model' transform, but may
        /// have others added for special case needs (e.g. kerbalEVA uses 'model' and 'model01' transforms, which would be specified as 'rootNames="model,model01"').
        /// </summary>
        [KSPField]
        public string rootNames = "model";

        [KSPField]
        public int transformIndex = -1;

        /// <summary>
        /// The section label to display in the Recoloring GUI.  Only used if the part is recolorable.
        /// </summary>
        [KSPField]
        public string sectionName = "Recolorable";

        /// <summary>
        /// True/false if this module can be adjusted while in-flight.
        /// This enables the texture-set selection buttons while in flight mode, but does not enable recoloring GUI (that is a separate part-module).
        /// </summary>
        [KSPField]
        public bool canChangeInFlight = false;

        /// <summary>
        /// Current texture set.  ChooseOption UI widget is initialized inside of texture-set-container helper object
        /// </summary>
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Texture Set"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentTextureSet = String.Empty;

        /// <summary>
        /// Persistent data storage field used to store custom recoloring data
        /// </summary>
        [KSPField(isPersistant = true)]
        public string persistentData = string.Empty;

        [Persistent]
        public string configNodeData = string.Empty;

        protected TextureSetContainer textureSets;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize();
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            initialize();
            Callback<BaseField, System.Object> onChangeAction = (BaseField a, System.Object b) =>
            {
                enableTextureSet(currentTextureSet, true, true);
            };
            BaseField field = Fields[nameof(currentTextureSet)];
            field.uiControlEditor.onFieldChanged = onChangeAction;
            field.uiControlFlight.onFieldChanged = onChangeAction;
            //set default values for UI enabled
            field.guiActive = canChangeInFlight;
            field.guiActiveEditor = true;
            //disable both if zero or one texture set exists
            if (textureSets.textureSets.Length <= 1)
            {
                field.guiActive = field.guiActiveEditor = false;
            }
        }

        /// <summary>
        /// Restores texture set data and either loads default texture set or saved texture set (if any)
        /// </summary>
        private void initialize()
        {
            if (textureSets != null)
            {
                //already initialized from OnLoad (prefab, some in-editor parts)
                return;
            }
            ConfigNode node = Utils.parseConfigNode(configNodeData);
            string[] setNames = node.GetStringValues("textureSet", false);
            string modelShaderName = node.GetStringValue("modelShader");

            List<TextureSet> allSets = new List<TextureSet>();
            if (!string.IsNullOrEmpty(modelShaderName))
            {
                TextureSet set = TexturesUnlimitedLoader.getModelShaderTextureSet(modelShaderName);
                if (set != null) { allSets.Add(set); }
            }
            TextureSet[] sets = TexturesUnlimitedLoader.getTextureSets(setNames);
            for (int i = 0; i < sets.Length; i++) { allSets.Add(sets[i]); }
            ConfigNode[] fullNodeSets = node.GetNodes("KSP_TEXTURE_SET");
            for (int i = 0; i < fullNodeSets.Length; i++)
            {
                allSets.Add(new TextureSet(fullNodeSets[i], "create"));
            }

            textureSets = new TextureSetContainer(this, Fields[nameof(currentTextureSet)], Fields[nameof(persistentData)], allSets);
            if (string.IsNullOrEmpty(currentTextureSet))
            {
                currentTextureSet = allSets[0].name;
            }
            this.updateUIChooseOptionControl(nameof(currentTextureSet), textureSets.getTextureSetNames(), textureSets.getTextureSetTitles(), true, currentTextureSet);
            textureSets.enableCurrentSet(getModelTransforms(), false);
            Fields[nameof(currentTextureSet)].guiName = sectionName;
        }

        /// <summary>
        /// Helper method to return either the specified named transforms from the model, or the model root transform if no trf name is specified in part config.
        /// </summary>
        /// <returns></returns>
        protected Transform[] getModelTransforms()
        {
            List<Transform> modelTransforms = new List<Transform>();
            Transform[] roots = getRootTransforms();
            int len = roots.Length;
            for (int i = 0; i < len; i++)
            {
                if (!string.IsNullOrEmpty(transformName))
                {
                    Transform[] trs = roots[i].FindChildren(transformName);
                    if (transformIndex >= 0)
                    {
                        modelTransforms.Add(trs[transformIndex]);
                    }
                    else
                    {
                        modelTransforms.AddRange(trs);
                    }
                }
                else
                {
                    modelTransforms.Add(roots[i]);
                }
            }
            return modelTransforms.ToArray();
        }

        /// <summary>
        /// Helper method to return the list of -root- transforms to use for this module.
        /// This should generally only be 'model', but in some special stock cases it may
        /// also include other transforms (e.g. 'model01' in evaKerbal part)
        /// </summary>
        /// <returns></returns>
        protected Transform[] getRootTransforms()
        {
            List<Transform> trs = new List<Transform>();
            string[] names = rootNames.Split(',');
            int len = names.Length;
            for (int i = 0; i < len; i++)
            {

                //special handling for editor as parts are parented to eachother through transforms
                if (HighLogic.LoadedSceneIsEditor)
                {
                    if (names.Length == 1 && names[0].Trim() == "model")
                    {
                        trs.Add(part.transform.FindRecursive("model"));
                    }
                    else
                    {
                        //TODO -- this causes issues in the editor with part hierarchy
                        //in the editor parts are parented to eachother through transforms, and this method picks up the
                        //'model' transforms from all children parts as well as the parent part.
                        Log.debug("TODO - fix part-hierarchy checking in editor when custom root transforms are specified.");
                        trs.AddRange(part.transform.FindChildren(names[i].Trim()));
                    }
                }
                else
                {
                    trs.AddRange(part.transform.FindChildren(names[i].Trim()));
                }
            }
            return trs.ToArray();
        }

        /// <summary>
        /// IRecolorable override.  Returns a string array containing the section name(s) for this texture-switch module.
        /// As a texture-switch only uses a single section, it returns only the 'section name' specified in the part config.
        /// </summary>
        /// <returns></returns>
        public string[] getSectionNames()
        {
            return new string[] { sectionName };
        }

        /// <summary>
        /// Return the user-specified recoloring values for the input section name.
        /// In this implementation the input name is ignored, and it returns the current user colors of this texture-switch module.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public RecoloringData[] getSectionColors(string name)
        {
            return textureSets.customColors;
        }
        
        /// <summary>
        /// Set the input recoloring colors to the texture set and update persistent data. 
        /// Input section name is ignored as texture switch module only has a single section.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="colors"></param>
        public void setSectionColors(string name, RecoloringData[] colors)
        {
            this.actionWithSymmetry(m => 
            {
                m.textureSets.setCustomColors(colors);
                m.textureSets.applyRecoloring(m.getModelTransforms(), colors);
            });
        }

        /// <summary>
        /// Return the current texture set assigned to the input section. 
        /// As the texture-switch module only has a single section, it always returns the currently active texture set.
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        public TextureSet getSectionTexture(string section)
        {
            return textureSets.currentTextureSet;
        }

        /// <summary>
        /// Singular 'enable texture set' function to be used by internal and external plugin code.  Optionally calls recursively
        /// to update symmetry counterparts if 'symmetry' parameter is true.<para/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="symmetry"></param>
        public void enableTextureSet(string name, bool symmetry, bool userInput)
        {
            if (textureSets == null) { return; }
            //TODO - validate input texture set name; log error if invalid
            currentTextureSet = name;
            textureSets.enableCurrentSet(getModelTransforms(), userInput);
            TextureCallbacks.onTextureSetChanged(part);
            if (symmetry)
            {
                this.actionWithSymmetry(m => 
                {
                    m.enableTextureSet(name, false, userInput);
                });
            }
        }

        /// <summary>
        /// Callback from Unity GameObject.SendMessage("onPartGeometryChanged")<para/>
        /// Sent by KSPWheel on KSPWheelSidedModel model changes to re-initialize texture sets.<para/>
        /// May optionally be called by any other mods that need to reapply the current texture set.<para/>
        /// Same message is used by FAR for voxel rebuilding, so this might change to something else in the future.
        /// </summary>
        public void onPartGeometryChanged()
        {
            enableTextureSet(currentTextureSet, false, false);
        }

        /// <summary>
        /// Callback from GameObject.SendMessage("changeTextureSet", "textureSetNameGoesHere")<para/>
        /// Currently unutilized; added for completion.  TODO - verify that system.object is correct type for input param (or can use string?)
        /// </summary>
        /// <param name="setNameString"></param>
        public void changeTextureSet(object setNameString)
        {
            string name = setNameString as string;
            if (name == null)
            {
                return;
            }
            enableTextureSet(name, false, false);
        }

    }

    /// <summary>
    /// Support/helper class for stand-alone texture switching (not used with model switching).
    /// Manages loading of texture sets, updating of color persistent data, and applying texture sets to model transforms
    /// </summary>
    public class TextureSetContainer
    {

        private PartModule pm;
        private BaseField textureSetField;
        private BaseField persistentDataField;

        internal TextureSet[] textureSets;

        internal RecoloringData[] customColors;

        private string currentTextureSetName
        {
            get { return (string)textureSetField.GetValue(pm); }
        }
        
        public TextureSet currentTextureSet
        {
            get
            {
                TextureSet set = Array.Find(textureSets, m => m.name == currentTextureSetName);
                if (set == null)
                {
                    Log.error("ERROR: KSPTextureSwitch could not locate texture set for name: " + currentTextureSetName+" on part: "+pm.part.name);
                }
                return set;
            }
        }

        private string persistentData
        {
            get { return (string)persistentDataField.GetValue(pm); }
            set { persistentDataField.SetValue(value, pm); }
        }

        public TextureSetContainer(PartModule pm, BaseField textureSetField, BaseField persistentDataField, List<TextureSet> sets)
        {
            this.pm = pm;
            this.textureSetField = textureSetField;
            this.persistentDataField = persistentDataField;
            loadPersistentData(persistentData);
            this.textureSets = sets.ToArray();
        }

        /// <summary>
        /// Updates the internal stored values and persistent values for recoloring data.  Does NOT apply the new colors.
        /// </summary>
        /// <param name="colors"></param>
        public void setCustomColors(RecoloringData[] colors)
        {
            customColors = colors;
            saveColors(customColors);
        }

        /// <summary>
        /// Apply the current texture to the input transforms.  The texture sets include/exclude settings will be used to determine what children of the input transforms should be adjusted.
        /// </summary>
        /// <param name="roots"></param>
        public void enableCurrentSet(Transform[] roots, bool userInput)
        {
            TextureSet set = currentTextureSet;
            if (set == null)
            {
                return;
            }
            if (userInput || customColors == null || customColors.Length == 0)
            {
                customColors = new RecoloringData[3];
                customColors[0] = set.maskColors[0];
                customColors[1] = set.maskColors[1];
                customColors[2] = set.maskColors[2];
            }
            int len = roots.Length;
            for (int i = 0; i < len; i++)
            {
                set.enable(roots[i], customColors);
            }
            saveColors(customColors);
        }

        /// <summary>
        /// Apply the current recoloring selections to the input transform.  Does not recreate material, but simply applies the properties to the current material.
        /// </summary>
        /// <param name="root"></param>
        /// <param name="userColors"></param>
        public void applyRecoloring(Transform root, RecoloringData[] userColors)
        {
            TextureSet set = currentTextureSet;
            if (set == null)
            {
                return;
            }
            set.applyRecoloring(root, userColors);
        }

        /// <summary>
        /// Apply the current recoloring selections to the input transforms.  Does not recreate material, but simply applies the properties to the current material.
        /// </summary>
        /// <param name="roots"></param>
        /// <param name="userColors"></param>
        public void applyRecoloring(Transform[] roots, RecoloringData[] userColors)
        {
            int len = roots.Length;
            for (int i = 0; i < len; i++)
            {
                applyRecoloring(roots[i], userColors);
            }
        }

        public string[] getTextureSetNames()
        {
            int len = textureSets.Length;
            string[] names = new string[len];
            for (int i = 0; i < len; i++)
            {
                names[i] = textureSets[i].name;
            }
            return names;
        }

        public string[] getTextureSetTitles()
        {
            int len = textureSets.Length;
            string[] names = new string[len];
            for (int i = 0; i < len; i++)
            {
                names[i] = textureSets[i].title;
            }
            return names;
        }

        private void loadPersistentData(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                string[] colorSplits = data.Split(';');
                int len = colorSplits.Length;
                customColors = new RecoloringData[len];
                for (int i = 0; i < len; i++)
                {
                    customColors[i] = RecoloringData.ParsePersistence(colorSplits[i]);
                }
            }
            else
            {
                customColors = new RecoloringData[0];
            }
        }

        private void saveColors(RecoloringData[] colors)
        {
            if (colors == null || colors.Length == 0) { return; }
            int len = colors.Length;
            string data = string.Empty;
            for (int i = 0; i < len; i++)
            {
                if (i > 0) { data = data + ";"; }
                data = data + colors[i].getPersistentData();
            }
            persistentData = data;
        }

    }

}

