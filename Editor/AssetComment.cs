using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GreatClock.Common.AssetAnalyser {

	public static class AssetComment {

		public static GUIContent GetAssetComment(Object asset, bool ignoreSpriteNPOT, out bool bold, out bool warning) {
			bold = false;
			warning = false;
			GUIContent comment = null;
			if (asset is Texture2D) {
				Texture2D tex = asset as Texture2D;
				int w = tex.width;
				int h = tex.height;
				bool pot = (w & (w - 1)) == 0 && (h & (h - 1)) == 0;
				string size = string.Format("{0}  {1}x{2}  {3}", pot ? "POT" : "NPOT", w, h,
					EditorUtility.FormatBytes(GetStorageMemorySizeLong(tex)));
				bold = tex.mipmapCount > 1;
				comment = new GUIContent(size);
				warning = !pot;
			} else if (asset is Texture) {
				Texture tex = asset as Texture;
				comment = new GUIContent(EditorUtility.FormatBytes(GetStorageMemorySizeLong(tex)));
			} else if (asset is Sprite) {
				Sprite sprite = asset as Sprite;
				Rect rect = sprite.rect;
				bool pot = true;
				if (!ignoreSpriteNPOT) {
					Texture2D tex = sprite.texture;
					int w = tex.width;
					int h = tex.height;
					pot = (w & (w - 1)) == 0 && (h & (h - 1)) == 0;
				}
				comment = new GUIContent(string.Format(pot ? "Texture NPOT  {0}x{1}" : "{0}x{1}", rect.width, rect.height));
			} else if (asset is Mesh) {
				Mesh mesh = asset as Mesh;
				cached_colors.Clear();
				int verts = mesh.vertexCount;
				mesh.GetColors(cached_colors);
				bool c = cached_colors.Count == verts;
				int uvs = 0;
				for (int i = 0; i < 8; i++) {
					cached_uvs.Clear();
					mesh.GetUVs(i, cached_uvs);
					if (cached_uvs.Count == verts) { uvs++; }
				}
				int tri = 0;
				for (int i = mesh.subMeshCount - 1; i >= 0; i--) {
					cached_triangles.Clear();
					mesh.GetTriangles(cached_triangles, i);
					tri += cached_triangles.Count;
				}
				cached_bone_weight.Clear();
				mesh.GetBoneWeights(cached_bone_weight);
				bool skin = cached_bone_weight.Count == verts;
				comment = new GUIContent(string.Format("v:{0} t:{1} uv:{2}{3}{4}",
					mesh.vertexCount, tri / 3, uvs, c ? " col" : "", skin ? " skin" : ""));
			} else if (asset is AnimationClip) {
				AnimationClip clip = asset as AnimationClip;
				comment = new GUIContent(string.Format("{0}s {1} curves:{2}", ToFixedString(clip.length, 3),
					clip.wrapMode, AnimationUtility.GetCurveBindings(clip).Length));
				bold = clip.legacy;
			} else if (asset is AudioClip) {
				AudioClip clip = asset as AudioClip;
				AudioImporter ai = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter;
				float len = clip.length;
				string length = len > 60f ?
					string.Format("{0}:{1:00.000}", Mathf.Floor(len / 60f), len % 60f) :
					string.Format("{0:00.000}", len);
				comment = new GUIContent(string.Format("{0} {1}kHz {2}chs {3}",
					length, clip.frequency / 1000f, clip.channels, ai.defaultSampleSettings.compressionFormat));
			} else if (asset is GameObject) {
				cached_trans.Clear();
				(asset as GameObject).GetComponentsInChildren<Transform>(true, cached_trans);
				int nodes = cached_trans.Count;
				cached_trans.Clear();
				(asset as GameObject).GetComponentsInChildren<Transform>(false, cached_trans);
				comment = new GUIContent(string.Format("{0} nodes, {1} inactived", nodes, nodes - cached_trans.Count));
			} else if (asset is Material) {
				string shader = (asset as Material).shader.name;
				comment = new GUIContent(shader, shader);
			} else {
				comment = new GUIContent();
			}
			return comment;
		}

		private static List<Vector2> cached_uvs = new List<Vector2>();
		private static List<Color> cached_colors = new List<Color>();
		private static List<int> cached_triangles = new List<int>();
		private static List<BoneWeight> cached_bone_weight = new List<BoneWeight>();

		private static List<Transform> cached_trans = new List<Transform>();

		private static object[] cached_parameter_1 = new object[1];

		private static MethodInfo get_texture_storage_size;
		private static long GetStorageMemorySizeLong(Texture t) {
			if (get_texture_storage_size == null) {
				System.Type type = System.Type.GetType("UnityEditor.TextureUtil,UnityEditor");
				get_texture_storage_size = type.GetMethod("GetStorageMemorySizeLong", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (get_texture_storage_size == null) { return 0L; }
			cached_parameter_1[0] = t;
			return (long)get_texture_storage_size.Invoke(null, cached_parameter_1);
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
