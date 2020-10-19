using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GreatClock.Common.AssetAnalyser {

	public abstract class AssetDependenciesCollecter : EditorWindow {

		protected static void CollectDependenciesInPrefab(out PrefabReferenceData[] prds, out AssetReferencedData[] ards) {
			string[] all = AssetDatabase.GetAllAssetPaths();
			float step1 = 0.3f;
			int len = all.Length;
			List<AssetReferencedData> ars = new List<AssetReferencedData>(2048);
			Dictionary<int, AssetReferencedData> instanceId2Asset = new Dictionary<int, AssetReferencedData>(1024);
			for (int i = 0; i < len; i++) {
				string path = all[i];
				float t = Mathf.Lerp(0f, step1, (i + 0.5f) / len);
				EditorUtility.DisplayProgressBar("Collecting Dependencies",
					string.Format("Step 1 ({0} / {1}) {2}", i + 1, len, path), t);
				if (AssetDatabase.IsValidFolder(path)) { continue; }
				if (!path.StartsWith("Assets/")) { continue; }
				if (path.LastIndexOf(".unity", System.StringComparison.OrdinalIgnoreCase) == path.Length - 6) { continue; }
				AssetReferencedData ard = new AssetReferencedData();
				ard.path = path;
				ard.asset = AssetDatabase.LoadMainAssetAtPath(path);
				ard.assets = AssetDatabase.LoadAllAssetsAtPath(path);
				ars.Add(ard);
				foreach (Object obj in ard.assets) {
					if (obj == null) { continue; }
					instanceId2Asset.Add(obj.GetInstanceID(), ard);
				}
			}
			string[] prefabs = AssetDatabase.FindAssets("t:prefab");
			len = prefabs.Length;
			List<PrefabReferenceData> prs = new List<PrefabReferenceData>(2048);
			Stack<Transform> trans = new Stack<Transform>();
			List<Component> components = new List<Component>();
			for (int i = 0; i < len; i++) {
				PrefabReferenceData prd = new PrefabReferenceData();
				prd.path = AssetDatabase.GUIDToAssetPath(prefabs[i]);
				prs.Add(prd);
				float t = Mathf.Lerp(step1, 1f, (i + 0.5f) / len);
				EditorUtility.DisplayProgressBar("Collecting Dependencies",
					string.Format("Step 2 ({0} / {1}) {2}", i + 1, len, prd.path), t);
				prd.asset = AssetDatabase.LoadMainAssetAtPath(prd.path);
				GameObject go = prd.asset as GameObject;
				if (go == null) { continue; }
				trans.Push(go.transform);
				while (trans.Count > 0) {
					Transform tt = trans.Pop();
					tt.GetComponents<Component>(components);
					foreach (Component component in components) {
						if (component == null || component is Transform) { continue; }
						SerializedObject so = new SerializedObject(component);
						SerializedProperty p = so.GetIterator();
						while (p.NextVisible(true)) {
							if (p.propertyType != SerializedPropertyType.ObjectReference) { continue; }
							AssetReferencedData ard;
							if (!instanceId2Asset.TryGetValue(p.objectReferenceInstanceIDValue, out ard)) { continue; }
							if (ard == null || ard.path == prd.path) { continue; }
							AddReferenceData(prd.path, prd.references, component, p, ard.path);
							ReferencedData rd = null;
							foreach (ReferencedData trd in ard.referenced) {
								if (trd.mainObject == prd.asset) {
									rd = trd;
									break;
								}
							}
							if (rd == null) {
								rd = new ReferencedData();
								rd.mainObject = prd.asset;
								ard.referenced.Add(rd);
							}
							AddReferenceData(prd.path, rd.references, component, p, ard.path);
						}
					}
					components.Clear();
					for (int j = tt.childCount - 1; j >= 0; j--) {
						trans.Push(tt.GetChild(j));
					}
				}
			}
			EditorUtility.ClearProgressBar();
			prds = prs.ToArray();
			ards = ars.ToArray();
		}

		private static List<string> node_path_gen = new List<string>(16);
		private static void AddReferenceData(string path, List<ReferenceData> references, Component component, SerializedProperty p, string dp) {
			ReferenceData rd = null;
			Transform node = component.transform;
			foreach (ReferenceData trd in references) {
				if (trd.node == node) {
					rd = trd;
					break;
				}
			}
			if (rd == null) {
				rd = new ReferenceData();
				rd.node = node;
				node_path_gen.Clear();
				Transform t = node;
				while (t != null) { node_path_gen.Add(t.name); t = t.parent; }
				node_path_gen.Reverse();
				rd.nodePath = string.Join("/", node_path_gen.ToArray());
				references.Add(rd);
			}
			ReferenceComponent rc = null;
			foreach (ReferenceComponent trc in rd.components) {
				if (trc.component == component) {
					rc = trc;
					break;
				}
			}
			if (rc == null) {
				rc = new ReferenceComponent();
				rc.component = component;
				rc.componentName = component.GetType().FullName;
				rd.components.Add(rc);
			}
			ReferenceProperty rp = new ReferenceProperty();
			rp.propertyPath = new GUIContent(p.propertyPath, string.Format("Asset Path :\n  {0}\n\nNode Path :\n  {1}\n\nComponent :\n  {2}\n\nProperty Path :\n  {3}\n\nDependency Asset :\n  {4}",
				path, rd.nodePath, rc.componentName, p.propertyPath, dp));
			rp.asset = p.objectReferenceValue;
			rp.comment = AssetComment.GetAssetComment(rp.asset, true, out rp.commentBold, out rp.commentWarning);
			rc.properties.Add(rp);
		}

		protected class PrefabReferenceData {
			public string path;
			public Object asset;
			public List<ReferenceData> references = new List<ReferenceData>();
			public int from;
		}

		protected class AssetReferencedData {
			public string path;
			public Object asset;
			public Object[] assets;
			public List<ReferencedData> referenced = new List<ReferencedData>();
			public int from;
		}

		protected class ReferenceData {
			public Transform node;
			public string nodePath;
			public List<ReferenceComponent> components = new List<ReferenceComponent>();
		}

		protected class ReferenceComponent {
			public Component component;
			public string componentName;
			public List<ReferenceProperty> properties = new List<ReferenceProperty>();
		}

		protected class ReferenceProperty {
			public GUIContent propertyPath;
			public Object asset;
			public GUIContent comment;
			public bool commentBold;
			public bool commentWarning;
		}

		protected class ReferencedData {
			public Object mainObject;
			public List<ReferenceData> references = new List<ReferenceData>();
		}

	}

}
