using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.U2D;

namespace GreatClock.Common.AssetAnalyser {

	public class FilterObject {

		private const string BLANK_CHARS = "\t\r\n ";
		private const string ESC_CHARS = " \t\\";
		private const string ESCED_CHARS = " \t/";

		public enum eTypes { Default, Types, AnimationClip, AudioClip, AudioMixer, Avatar, Controller, GameObject, Material, Mesh, PhysicMaterial, PhysicMaterial2D, Scene, Script, Shader, Sprite, SpriteAtlas, TextAsset, Texture }

		public FilterObject() {
			int max = 0;
			System.Array values = System.Enum.GetValues(typeof(eTypes));
			foreach (object obj in values) {
				max = Mathf.Max((int)obj, max);
			}
			mKeyWords = new List<string>[max + 1];
			foreach (object obj in values) {
				mKeyWords[(int)obj] = new List<string>();
			}
		}

		private string mFilterString;
		public string FilterString {
			get {
				return mFilterString;
			}
			set {
				if (mFilterString == value) { return; }
				mFilterString = value;
				for (int i = mKeyWords.Length - 1; i >= 0; i--) {
					List<string> keywords = mKeyWords[i];
					if (keywords != null) { keywords.Clear(); }
				}
				mFilterTypes.Clear();
				if (string.IsNullOrEmpty(mFilterString)) { return; }
				mEscIndices.Clear();
				string error = null;
				int len = mFilterString.Length;
				eTypes keyWordType = eTypes.Default;
				bool esc = false;
				int start = 0;
				int index = 0;
				while (true) {
					char chr = mFilterString[index];
					int end = -1;
					if (esc) {
						int ii = ESC_CHARS.IndexOf(chr);
						if (ii < 0) {
							error = string.Format("Char '{0}' cannot be eacaped. At {1} of '{2}'",
								chr, index, mFilterString);
							break;
						}
						mEscIndices.Push(new KeyValuePair<int, char>(index - start - 1, ESCED_CHARS[ii]));
						esc = false;
					} else if (chr == '\\') {
						esc = true;
					} else if (chr == ':' || BLANK_CHARS.IndexOf(chr) >= 0) {
						end = index;
					}
					index++;
					if (index >= len && end < 0) {
						end = index;
					}
					if (end > start) {
						string str = mFilterString.Substring(start, end - start);
						while (mEscIndices.Count > 0) {
							KeyValuePair<int, char> escIndexAndChar = mEscIndices.Pop();
							str = string.Concat(str.Substring(0, escIndexAndChar.Key), escIndexAndChar.Value,
								str.Substring(escIndexAndChar.Key + 2, str.Length - escIndexAndChar.Key - 2));
						}
						if (chr == ':') {
							if (keyWordType != eTypes.Default) {
								error = string.Format("Unexpected ':' at index '{0}' of '{1}' !", index, mFilterString);
								break;
							}
							switch (str) {
								case "t":
								case "type":
									keyWordType = eTypes.Types;
									break;
								case "anim":
								case "animationclip":
									keyWordType = eTypes.AnimationClip;
									break;
								case "audio":
								case "audioclip":
									keyWordType = eTypes.AudioClip;
									break;
								case "audiomixer":
									keyWordType = eTypes.AudioMixer;
									break;
								case "avatar":
									keyWordType = eTypes.Avatar;
									break;
								case "controller":
								case "animatorcontroller":
									keyWordType = eTypes.Controller;
									break;
								case "prefab":
								case "gameobject":
									keyWordType = eTypes.GameObject;
									break;
								case "mat":
								case "material":
									keyWordType = eTypes.Material;
									break;
								case "mesh":
									keyWordType = eTypes.Mesh;
									break;
								case "scene":
									keyWordType = eTypes.Scene;
									break;
								case "phymat":
								case "physicmat":
								case "physicmaterial":
									keyWordType = eTypes.PhysicMaterial;
									break;
								case "phymat2d":
								case "physicmat2d":
								case "physicmaterial2d":
									keyWordType = eTypes.PhysicMaterial2D;
									break;
								case "script":
								case "mono":
								case "monoscript":
									keyWordType = eTypes.Script;
									break;
								case "shader":
									keyWordType = eTypes.Shader;
									break;
								case "sprite":
									keyWordType = eTypes.Sprite;
									break;
								case "atlas":
								case "spriteatlas":
									keyWordType = eTypes.SpriteAtlas;
									break;
								case "text":
								case "textasset":
									keyWordType = eTypes.TextAsset;
									break;
								case "tex":
								case "texture":
									keyWordType = eTypes.Texture;
									break;
							}
						} else {
							if (!string.IsNullOrEmpty(str)) {
								mKeyWords[(int)keyWordType].Add(str);
							}
							keyWordType = eTypes.Default;
						}
						start = index;
					}
					if (index >= len) { break; }
				}
				if (error != null) {
					for (int i = mKeyWords.Length - 1; i >= 0; i--) {
						List<string> keywords = mKeyWords[i];
						if (keywords != null) { keywords.Clear(); }
					}
					Debug.LogError(error);
				} else {
					/*System.Array values = System.Enum.GetValues(typeof(eTypes));
					foreach (object obj in values) {
						List<string> keywords = mKeyWords[(int)obj];
						Debug.LogWarningFormat("{0} : {1}", obj, string.Join(", ", keywords.ToArray()));
					}*/
					List<string> types = mKeyWords[(int)eTypes.Types];
					foreach (string type in types) {
						switch (type.ToLower()) {
							case "anim":
							case "animationclip":
								mFilterTypes.Add(typeof(AnimationClip));
								break;
							case "audio":
							case "audioclip":
								mFilterTypes.Add(typeof(AudioClip));
								break;
							case "audiomixer":
								mFilterTypes.Add(typeof(AudioMixer));
								break;
							case "avatar":
								mFilterTypes.Add(typeof(Avatar));
								break;
							case "controller":
							case "animatorcontroller":
								mFilterTypes.Add(typeof(AnimatorController));
								break;
							case "prefab":
							case "gameobject":
								mFilterTypes.Add(typeof(GameObject));
								break;
							case "mat":
							case "material":
								mFilterTypes.Add(typeof(Material));
								break;
							case "mesh":
								mFilterTypes.Add(typeof(Mesh));
								break;
							case "scene":
								mFilterTypes.Add(typeof(SceneAsset));
								break;
							case "phymat":
							case "physicmat":
							case "physicsmat":
							case "physicmaterial":
							case "physicsmaterial":
								mFilterTypes.Add(typeof(PhysicMaterial));
								break;
							case "phymat2d":
							case "physicmat2d":
							case "physiscmat2d":
							case "physicmaterial2d":
							case "physicsmaterial2d":
								mFilterTypes.Add(typeof(PhysicsMaterial2D));
								break;
							case "script":
							case "mono":
							case "monoscript":
								mFilterTypes.Add(typeof(MonoScript));
								break;
							case "shader":
								mFilterTypes.Add(typeof(Shader));
								break;
							case "sprite":
								mFilterTypes.Add(typeof(Sprite));
								break;
							case "atlas":
							case "spriteatlas":
								mFilterTypes.Add(typeof(SpriteAtlas));
								break;
							case "text":
							case "textasset":
								mFilterTypes.Add(typeof(TextAsset));
								break;
							case "tex":
							case "texture":
								mFilterTypes.Add(typeof(Texture));
								break;
							case "tex2d":
							case "texture2d":
								mFilterTypes.Add(typeof(Texture2D));
								break;
						}
					}
				}
			}
		}

		public bool GetTypeRestrict(eTypes type) {
			return mKeyWords[(int)type].Count > 0;
		}

		public int GetRestrictTypes(List<eTypes> types) {
			int ret = 0;
			System.Array values = System.Enum.GetValues(typeof(eTypes));
			foreach (object obj in values) {
				int index = (int)obj;
				if (index == (int)eTypes.Default || index == (int)eTypes.Types) { continue; }
				if (mKeyWords[index].Count > 0) {
					types.Add((eTypes)index);
					ret++;
				}
			}
			return ret;
		}

		public eTypes GetObjectType(Object obj) {
			if (obj is AnimationClip) { return eTypes.AnimationClip; }
			if (obj is AudioClip) { return eTypes.AudioClip; }
			if (obj is AudioMixer) { return eTypes.AudioMixer; }
			if (obj is Avatar) { return eTypes.Avatar; }
			if (obj is AnimatorController) { return eTypes.Controller; }
			if (obj is GameObject) { return eTypes.GameObject; }
			if (obj is Material) { return eTypes.Material; }
			if (obj is Mesh) { return eTypes.Mesh; }
			if (obj is PhysicMaterial) { return eTypes.PhysicMaterial; }
			if (obj is PhysicsMaterial2D) { return eTypes.PhysicMaterial2D; }
			if (obj is SceneAsset) { return eTypes.Scene; }
			if (obj is MonoScript) { return eTypes.Script; }
			if (obj is Shader) { return eTypes.Shader; }
			if (obj is Sprite) { return eTypes.Sprite; }
			if (obj is SpriteAtlas) { return eTypes.SpriteAtlas; }
			if (obj is TextAsset) { return eTypes.TextAsset; }
			if (obj is Texture) { return eTypes.Texture; }
			return eTypes.Default;
		}

		public bool Filter(Object obj, bool isMain) {
			if (obj == null || obj.Equals(null)) { return isMain; }
			string path = AssetDatabase.GetAssetPath(obj);
			if (string.IsNullOrEmpty(path)) { path = obj.name; }
			if (isMain) {
				if (mFilterTypes.Count > 0) {
					System.Type type = obj.GetType();
					bool flag = false;
					for (int i = mFilterTypes.Count - 1; i >= 0; i--) {
						if (type.Equals(mFilterTypes[i]) || type.IsSubclassOf(mFilterTypes[i])) {
							flag = true;
							break;
						}
					}
					if (!flag) { return false; }
				}
				return FilterContent(path, mKeyWords[(int)eTypes.Default], true);
			}
			eTypes et = GetObjectType(obj);
			if (et == eTypes.Default) { return true; }
			List<string> filters = mKeyWords[(int)et];
			return FilterContent(path, filters, false);
		}

		private bool FilterContent(string content, List<string> filters, bool emptypass) {
			if (filters == null || filters.Count <= 0) { return emptypass; }
			for (int i = filters.Count - 1; i >= 0; i--) {
				if (content.IndexOf(filters[i], System.StringComparison.OrdinalIgnoreCase) >= 0) {
					return true;
				}
			}
			return false;
		}

		private Stack<KeyValuePair<int, char>> mEscIndices = new Stack<KeyValuePair<int, char>>();

		private List<string>[] mKeyWords;
		private List<System.Type> mFilterTypes = new List<System.Type>();

	}

}
