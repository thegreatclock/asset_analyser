using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GreatClock.Common.AssetAnalyser {

	public class TextureSizeAssessment : EditorWindow, IHasCustomMenu {

		[MenuItem("GreatClock/Asset Analyser/TextureSizeAssessment")]
		static void TestTextureSizeAssessment() {
			string[] prefabs = AssetDatabase.FindAssets("t:Prefab");
			List<GameObjectData> objects = new List<GameObjectData>();
			for (int i = 0, imax = prefabs.Length; i < imax; i++) {
				//if (i > 50) { break; }
				string p = AssetDatabase.GUIDToAssetPath(prefabs[i]);
				//if (!p.Contains("jiaonang")) { continue; }
				EditorUtility.DisplayProgressBar("Collecting Texture Usages",
					string.Format("({0}/{1}) {2}", i + 1, imax, p),
					(i + 0.5f) / imax);
				GameObjectData gd = AssessGameObject(AssetDatabase.LoadAssetAtPath<GameObject>(p));
				objects.Add(gd);
				gd.path = p;
			}
			EditorUtility.ClearProgressBar();
			TextureSizeAssessment win = GetWindow<TextureSizeAssessment>(false, "Texture Assessment");
			win.SetObjects(objects);
			win.minSize = new Vector2(940f, 300f);
			win.maxSize = new Vector2(940f, 4000f);
			win.Show();
		}

		#region core

		private static List<Vector3> vertices = new List<Vector3>();
		private static List<Vector2> uvs = new List<Vector2>();
		private static List<int> triangles = new List<int>();
		static GameObjectData AssessGameObject(GameObject go) {
			if (go == null) { return null; }
			GameObjectData gd = new GameObjectData();
			gd.gameobject = go;
			foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) {
				Mesh mesh = null;
				if (r is SkinnedMeshRenderer) {
					mesh = (r as SkinnedMeshRenderer).sharedMesh;
				} else {
					MeshFilter mf = r.GetComponent<MeshFilter>();
					if (mf != null) { mesh = mf.sharedMesh; }
				}
				if (mesh == null) { continue; }
				RendererData rd = new RendererData();
				rd.renderer = r;
				gd.renderers.Add(rd);
				Material[] mats = r.sharedMaterials;
				Transform trans = r.transform;
				vertices.Clear();
				uvs.Clear();
				mesh.GetVertices(vertices);
				mesh.GetUVs(0, uvs);
				for (int i = 0; i < mesh.subMeshCount; i++) {
					Material m = i < mats.Length ? mats[i] : null;
					MaterialData md = new MaterialData();
					md.material = m;
					rd.materials.Add(md);
					if (m != null) {
						string mp = AssetDatabase.GetAssetPath(m);
						if (string.IsNullOrEmpty(mp)) { continue; }
						foreach (string tp in AssetDatabase.GetDependencies(mp, false)) {
							Texture tex = AssetDatabase.LoadAssetAtPath<Texture>(tp);
							if (tex == null) { continue; }
							TextureData td = new TextureData();
							td.texture = tex;
							md.textures.Add(td);
						}
					}
					if (uvs.Count > 0) {
						md.duv_min = float.PositiveInfinity;
						float weight = 0f;
						triangles.Clear();
						mesh.GetTriangles(triangles, i, false);
						int index = 0;
						//Debug.LogError(triangles.Count);
						while (index < triangles.Count) {
							int i0 = triangles[index++];
							int i1 = triangles[index++];
							int i2 = triangles[index++];
							Vector3 v0 = vertices[i0];
							Vector3 v1 = vertices[i1];
							Vector3 v2 = vertices[i2];
							Vector2 uv0 = uvs[i0];
							Vector2 uv1 = uvs[i1];
							Vector2 uv2 = uvs[i2];
							//Debug.LogWarningFormat("{0} - {1} - {2}", v0, v1, v2);
							v0 = trans.TransformPoint(v0);
							v1 = trans.TransformPoint(v1);
							v2 = trans.TransformPoint(v2);
							//Debug.LogWarningFormat("{0} - {1} - {2}", v0, v1, v2);
							//Debug.LogWarningFormat("{0} - {1} - {2}", uv0, uv1, uv2);
							float dis0 = (v1 - v0).magnitude;
							float dis1 = (v2 - v1).magnitude;
							float dis2 = (v0 - v2).magnitude;
							float duv0 = (uv1 - uv0).magnitude / dis0;
							float duv1 = (uv2 - uv1).magnitude / dis1;
							float duv2 = (uv0 - uv2).magnitude / dis2;
							float s = 0.25f * Mathf.Sqrt((dis0 + dis1 + dis2) * (dis0 + dis1 - dis2) * (dis0 + dis2 - dis1) * (dis1 + dis2 - dis0));
							md.duv_min = Mathf.Min(md.duv_min, duv0);
							md.duv_min = Mathf.Min(md.duv_min, duv1);
							md.duv_min = Mathf.Min(md.duv_min, duv2);
							md.duv_max = Mathf.Max(md.duv_max, duv0);
							md.duv_max = Mathf.Max(md.duv_max, duv1);
							md.duv_max = Mathf.Max(md.duv_max, duv2);
							md.duv_avg += (duv0 + duv1 + duv2) * s;
							weight += s + s + s;
						}
						md.duv_avg /= weight;
					}
					//Debug.LogError(md.duv_min);
					//Debug.LogWarning(md.duv_max);
					//Debug.LogWarning(md.duv_avg);
					//Debug.Log(weight);
				}
			}
			return gd;
		}

		private class GameObjectData {
			public string path;
			public GameObject gameobject;
			public List<RendererData> renderers = new List<RendererData>();
			//for gui use
			public int from;
		}

		private class RendererData {
			public Renderer renderer;
			public List<MaterialData> materials = new List<MaterialData>();
		}

		private class MaterialData {
			public Material material;
			public float duv_min;
			public float duv_max;
			public float duv_avg;
			public List<TextureData> textures = new List<TextureData>();
			private string m_duv_info;
			public string duv_info {
				get {
					if (string.IsNullOrEmpty(m_duv_info)) {
						m_duv_info = string.Format("{0}    {1}    {2}",
							ToFixedString(duv_min, 5), ToFixedString(duv_max, 5), ToFixedString(duv_avg, 5));
					}
					return m_duv_info;
				}
			}
		}

		private class TextureData {
			public Texture texture;
			private float m_duv_min = -1f;
			private float m_duv_max = -1f;
			private float m_duv_avg = -1f;
			private float m_dpx_avg;
			private string m_dpx_info;
			public string GetDPXInfo(float duv_min, float duv_max, float duv_avg, out float dpx_avg) {
				if (m_duv_min != duv_min || m_duv_max != duv_max || m_duv_avg != duv_avg) {
					Texture2D tex = texture as Texture2D;
					float size = tex == null ? 0f : Mathf.Sqrt(tex.width * tex.width + tex.height * tex.height);
					m_dpx_info = size <= 0f ? "" : string.Format("{0}    {1}    {2}", ToFixedString(duv_min * size, 5),
						ToFixedString(duv_max * size, 5), ToFixedString(duv_avg * size, 5));
					m_duv_min = duv_min;
					m_duv_max = duv_max;
					m_duv_avg = duv_avg;
					m_dpx_avg = duv_avg * size;
				}
				dpx_avg = m_dpx_avg;
				return m_dpx_info;
			}
		}

		#endregion

		private static bool gui_inited = false;
		private static GUIStyle style_toolbar_search_text;
		private static GUIStyle style_toolbar_search_cancel;
		private static GUIStyle style_toolbar_button;
		private static GUIStyle style_cn_box;

		private static GUILayoutOption min_width;
		private static GUILayoutOption min_height;
		private static GUILayoutOption width_filter;
		private static GUILayoutOption width_64;
		private static GUILayoutOption width_prefab;
		private static GUILayoutOption width_renderer;
		private static GUILayoutOption width_material;
		private static GUILayoutOption width_uv_pm;
		private static GUILayoutOption width_texture;
		private static GUILayoutOption width_px_pm;

		private List<GameObjectData> mObjects;
		private List<GameObjectData> mFilteredObjects;

		private Vector2 mScroll;

		private void SetObjects(List<GameObjectData> objects) {
			mObjects = objects;
			FilterObjects();
		}

		private void FilterObjects() {
			if (mObjects == null) { return; }
			if (mFilteredObjects == null) {
				mFilteredObjects = new List<GameObjectData>();
			} else {
				mFilteredObjects.Clear();
			}
			foreach (GameObjectData gd in mObjects) {
				bool pass = mSearch.Filter(gd.gameobject, true);
				if (pass) {
					bool flagMat = !mSearch.GetTypeRestrict(FilterObject.eTypes.Material);
					bool flagShader = !mSearch.GetTypeRestrict(FilterObject.eTypes.Shader);
					bool flagTex = !mSearch.GetTypeRestrict(FilterObject.eTypes.Texture);
					foreach (RendererData rd in gd.renderers) {
						foreach (MaterialData md in rd.materials) {
							if (md.material != null) {
								if (!flagMat && mSearch.Filter(md.material, false)) {
									flagMat = true;
								}
								if (!flagShader && mSearch.Filter(md.material.shader, false)) {
									flagShader = true;
								}
							}
							foreach (TextureData td in md.textures) {
								if (!flagTex && mSearch.Filter(td.texture, false)) {
									flagTex = true;
								}
							}
							if (flagMat && flagShader && flagTex) { break; }
						}
						if (flagMat && flagShader && flagTex) { break; }
					}
					pass &= flagMat && flagShader && flagTex;
				}
				if (pass) { mFilteredObjects.Add(gd); }
			}
			int lines = 0;
			foreach (GameObjectData gd in mFilteredObjects) {
				gd.from = lines;
				foreach (RendererData rd in gd.renderers) {
					foreach (MaterialData md in rd.materials) {
						lines += Mathf.Max(1, md.textures.Count);
					}
				}
			}
		}

		private FilterObject mSearch = new FilterObject();

		void OnGUI() {
			InitGUI();
			EditorGUILayout.BeginHorizontal(GUILayout.Width(920f), min_height);
			GUILayout.Label("Filter : ", EditorStyles.miniLabel, width_filter);
			string search = GUILayout.TextField(mSearch.FilterString, style_toolbar_search_text);
			if (GUILayout.Button("", style_toolbar_search_cancel)) {
				search = "";
			}
			if (search != mSearch.FilterString) {
				mSearch.FilterString = search;
				FilterObjects();
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Prefab", style_toolbar_button, width_prefab);
			GUILayout.Label("Renderer", style_toolbar_button, width_renderer);
			GUILayout.Label("Material", style_toolbar_button, width_material);
			GUILayout.Label("UV per meter (min max average)", style_toolbar_button, width_uv_pm);
			GUILayout.Label("Texture", style_toolbar_button, width_texture);
			GUILayout.Label("Pxs per meter (min max average)", style_toolbar_button, width_px_pm);
			EditorGUILayout.EndHorizontal();
			mScroll = EditorGUILayout.BeginScrollView(mScroll, false, false);
			const float item_height = 20f;
			int from = Mathf.FloorToInt(mScroll.y / item_height);
			int end = Mathf.CeilToInt(position.height / item_height) + from;
			int len = mFilteredObjects == null ? 0 : mFilteredObjects.Count;
			for (int i = 0; i < len; i++) {
				GameObjectData gd = mFilteredObjects[i];
				if (gd.renderers.Count <= 0) { continue; }
				float standardDpx = GetStandardDpx();
				float standardDpx2x = standardDpx * 2f;
				float standardDpx4x = standardDpx * 4f;
				int gtexs = 0;
				foreach (RendererData rd in gd.renderers) {
					foreach (MaterialData md in rd.materials) {
						gtexs += Mathf.Max(1, md.textures.Count);
					}
				}
				if (gd.from + gtexs < from || gd.from > end) {
					GUILayout.Space(gtexs * item_height);
					continue;
				}
				GUILayout.BeginHorizontal(min_height);
				// prefab
				GUILayout.BeginVertical(style_cn_box, width_prefab);
				float spacep = item_height * (gtexs - 1) * 0.5f;
				GUILayout.Space(spacep + 2f);
				EditorGUILayout.ObjectField(gd.gameobject, typeof(GameObject), false);
				GUILayout.Space(spacep);
				GUILayout.EndVertical();
				// renderer
				GUILayout.BeginVertical(width_renderer);
				foreach (RendererData rd in gd.renderers) {
					int rtexs = 0;
					foreach (MaterialData md in rd.materials) {
						rtexs += Mathf.Max(1, md.textures.Count);
					}
					GUILayout.BeginVertical(style_cn_box);
					float space = item_height * (rtexs - 1) * 0.5f;
					GUILayout.Space(space + 2f);
					EditorGUILayout.ObjectField(rd.renderer, typeof(Renderer), false);
					GUILayout.Space(space);
					GUILayout.EndVertical();
				}
				GUILayout.EndVertical();
				// material
				GUILayout.BeginVertical(width_material);
				foreach (RendererData rd in gd.renderers) {
					foreach (MaterialData md in rd.materials) {
						int ttexs = md.textures.Count;
						GUILayout.BeginVertical(style_cn_box);
						if (ttexs <= 0) {
							EditorGUILayout.LabelField("  ", min_width);
						} else {
							float space = item_height * (ttexs - 1) * 0.5f;
							GUILayout.Space(space + 2f);
							EditorGUILayout.ObjectField(md.material, typeof(Material), false);
							GUILayout.Space(space);
						}
						GUILayout.EndVertical();
					}
				}
				GUILayout.EndVertical();
				// uv per meter
				GUILayout.BeginVertical(width_uv_pm);
				foreach (RendererData rd in gd.renderers) {
					foreach (MaterialData md in rd.materials) {
						int ttexs = md.textures.Count;
						GUILayout.BeginVertical(style_cn_box);
						if (ttexs <= 0) {
							EditorGUILayout.LabelField("  ", min_width);
						} else {
							float space = item_height * (ttexs - 1) * 0.5f;
							GUILayout.Space(space + 2f);
							EditorGUILayout.LabelField(md.duv_info, min_width);
							GUILayout.Space(space);
						}
						GUILayout.EndVertical();
					}
				}
				GUILayout.EndVertical();
				// texture
				GUILayout.BeginVertical(width_texture);
				foreach (RendererData rd in gd.renderers) {
					foreach (MaterialData md in rd.materials) {
						foreach (TextureData td in md.textures) {
							EditorGUILayout.BeginHorizontal(style_cn_box);
							EditorGUILayout.ObjectField(td.texture, typeof(Texture), false);
							Texture2D tex = td.texture as Texture2D;
							string size = string.Format("{0}x{1}", tex == null ? "?" : tex.width.ToString(),
								tex == null ? "?" : tex.height.ToString());
							EditorGUILayout.LabelField(size, tex != null && tex.mipmapCount > 1 ?
								EditorStyles.miniBoldLabel : EditorStyles.miniLabel, width_64);
							EditorGUILayout.EndHorizontal();
						}
						if (md.textures.Count <= 0) {
							GUILayout.BeginVertical(style_cn_box);
							EditorGUILayout.LabelField("  ", min_width);
							EditorGUILayout.EndHorizontal();
						}
					}
				}
				GUILayout.EndVertical();
				// pixels per meter
				GUILayout.BeginVertical(width_px_pm);
				foreach (RendererData rd in gd.renderers) {
					foreach (MaterialData md in rd.materials) {
						foreach (TextureData td in md.textures) {
							GUILayout.BeginVertical(style_cn_box);
							float dpx;
							string dpxInfo = td.GetDPXInfo(md.duv_min, md.duv_max, md.duv_avg, out dpx);
							Color cachedColor = GUI.color;
							if (dpx > standardDpx) {
								Color orange = new Color(1f, 0.5f, 0f, 1f);
								if (dpx > standardDpx2x) {
									float t = Mathf.InverseLerp(standardDpx2x, standardDpx4x, dpx);
									GUI.color = Color.Lerp(orange, Color.red, t);
								} else {
									float t = Mathf.InverseLerp(standardDpx, standardDpx2x, dpx);
									GUI.color = Color.Lerp(cachedColor, orange, t);
								}
							}
							EditorGUILayout.LabelField(dpxInfo, min_width);
							GUI.color = cachedColor;
							GUILayout.EndVertical();
						}
						if (md.textures.Count <= 0) {
							GUILayout.BeginVertical(style_cn_box);
							EditorGUILayout.LabelField("  ", min_width);
							GUILayout.EndVertical();
						}
					}
				}
				GUILayout.EndVertical();
				GUILayout.EndHorizontal();
			}
			EditorGUILayout.EndScrollView();
		}

		void InitGUI() {
			if (gui_inited) { return; }
			gui_inited = true;
			style_toolbar_search_text = "ToolbarSeachTextField";
			style_toolbar_search_cancel = "ToolbarSeachCancelButton";
			style_toolbar_button = "ToolbarButton";
			style_cn_box = "CN Box";
			min_width = GUILayout.MinWidth(8f);
			min_height = GUILayout.MinHeight(8f);
			width_filter = GUILayout.Width(36f);
			width_64 = GUILayout.Width(64f);
			width_prefab = GUILayout.Width(120f);
			width_renderer = GUILayout.Width(120f);
			width_material = GUILayout.Width(120f);
			width_uv_pm = GUILayout.Width(170f);
			width_texture = GUILayout.Width(220f);
			width_px_pm = GUILayout.Width(170f);
		}

		public void AddItemsToMenu(GenericMenu menu) {
			for (int i = 1; i <= 10; i++) {
				int level = i;
				string content = string.Format("Density Level/Level {0} ({1} px per meter)", level, GetLevelDpx(level));
				menu.AddItem(new GUIContent(content), i == DensityLevel, () => { DensityLevel = level; });
			}
		}

		private int mDensityLevel = -1;
		private int DensityLevel {
			get {
				if (mDensityLevel < 0) {
					mDensityLevel = EditorPrefs.GetInt("texture_density_level", 4);
				}
				return mDensityLevel;
			}
			set {
				if (mDensityLevel == value) { return; }
				mDensityLevel = value;
				EditorPrefs.SetInt("texture_density_level", mDensityLevel);
			}
		}

		private float GetStandardDpx() {
			return GetLevelDpx(DensityLevel);
		}

		private float GetLevelDpx(int level) {
			switch (level) {
				case 1: return 40f;
				case 2: return 60f;
				case 3: return 80f;
				case 4: return 100f;
				case 5: return 150f;
				case 6: return 200f;
				case 7: return 250f;
				case 8: return 300f;
				case 9: return 400f;
				case 10: return 500f;
			}
			return 100f;
		}

		static string ToFixedString(double val, int numbers) {
			double dns = System.Math.Log10(val);
			int ns = (int)dns;
			ns++;
			if (ns < 1) { ns = 1; }
			string format = string.Format("F{0}", numbers - ns);
			return val.ToString(format);
		}

	}

}
