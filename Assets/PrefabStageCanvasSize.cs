#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine;
using Resizer = PrefabStageCanvasSize.Resizer;

/// <summary>
/// Watches for state changes of prefab stage. 
/// Adds special component to the root game object when prefab gets open,
/// destroys it when prefab is being closed.
/// </summary>
[InitializeOnLoad]
static class PrefabStageCanvasSize
{
	static PrefabStageCanvasSize()
	{
		if (EditorApplication.isPlaying)
			return;

		PrefabStage.prefabStageOpened += stage =>
		{
			// get RectTranform of prefab environment
			var envRT = stage.prefabContentsRoot.transform.parent as RectTransform;
			if (envRT == null)
				return; // non UI prefab

			// although add/destroy of components is not prohibited
			// for environment game object, but it's not editable in inspector,
			// so we add component to root game object of prefab
			var resizer = stage.prefabContentsRoot.AddComponent<Resizer>();

			// to be sure it won't be saved in anyways
			resizer.hideFlags = HideFlags.DontSave;

			resizer.Init(envRT, envRT.GetComponent<Canvas>());
		};

		PrefabStage.prefabStageClosing += stage =>
		{
			// destroy component to avoid memory leaks when using DontSave flags
			var resizer = stage.prefabContentsRoot.GetComponent<Resizer>();
			UnityEngine.Object.DestroyImmediate(resizer);
		};
	}

	/// <summary>
	/// Changes environment canvas size according to chosen mode in the inspector.
	/// This class made nested to not show it up in [Add Component] drop down list.
	/// </summary>
	[ExecuteAlways]
	public class Resizer : MonoBehaviour
	{
		// change default values if needed
		public Vector2 ReferenceSize = new Vector2(1920, 1080);
		public Mode CanvasSizeMode = Mode.ExpandUsingGameViewAspect;

		public bool isInitialized { get; private set; }

		public enum Mode
		{
			ReferenceSize,
			HeightFollowsGameViewAspect,
			WidthFollowsGameViewAspect,
			ExpandUsingGameViewAspect,
			NativeBehaviour
		}

		// components of environment game object
		private RectTransform _envRect;
		private Canvas _envCanvas;

		public void Init(RectTransform envRect, Canvas envCanvas)
		{
			_envRect = envRect;
			_envCanvas = envCanvas;

			SetMode();
		}

		public void SetMode()
		{
			if (_envRect == null || _envCanvas == null)
				return;

			if (CanvasSizeMode == Mode.NativeBehaviour)
			{
				_envCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
				_envRect.sizeDelta = Handles.GetMainGameViewSize();
			}
			else
			{
				_envCanvas.renderMode = RenderMode.WorldSpace;

				if (CanvasSizeMode == Mode.ReferenceSize)
					_envRect.sizeDelta = ReferenceSize;
				else
					Update();
			}
		}

		private void Update()
		{
			// update environment rect transform size
			// when width or height is following Game View aspect ratio

			if (_envRect == null
			|| CanvasSizeMode == Mode.ReferenceSize
			|| CanvasSizeMode == Mode.NativeBehaviour)
				return;

			var game = Handles.GetMainGameViewSize();
			var size = ReferenceSize;

			switch (CanvasSizeMode)
			{
				case Mode.ExpandUsingGameViewAspect:
				{
					ReferenceSize.x = Math.Max(ReferenceSize.x, 10);
					ReferenceSize.y = Math.Max(ReferenceSize.y, 10);

					float refAspect = ReferenceSize.x / ReferenceSize.y;
					float gameAspect = game.x / game.y;

					if (refAspect < gameAspect)
						size.x *= gameAspect / refAspect;
					else
						size.y *= refAspect / gameAspect;
				}
				break;

				case Mode.HeightFollowsGameViewAspect:
					size.y = size.x * game.y / game.x;
					break;

				case Mode.WidthFollowsGameViewAspect:
					size.x = size.y * game.x / game.y;
					break;

				default:
					throw new NotImplementedException(CanvasSizeMode.ToString());
			}

			_envRect.sizeDelta = size;
		}
	}
}

/// <summary>
/// The purpose of this custom editor is to avoid dirtying of the prefab
/// caused by modifications of Resizer properties in the inspector.
/// </summary>
[CustomEditor(typeof(Resizer))]
public class PrefabStageCanvasSizeEditor : Editor
{
	private SerializedProperty _referenceSize, _canvasSizeMode;
	private static Resizer.Mode[] _modeValues;

	static PrefabStageCanvasSizeEditor()
	{
		_modeValues = (Resizer.Mode[])System.Enum.GetValues(typeof(Resizer.Mode));
	}

	public void OnEnable()
	{
		_referenceSize = serializedObject.FindProperty("ReferenceSize");
		_canvasSizeMode = serializedObject.FindProperty("CanvasSizeMode");
	}

	public override void OnInspectorGUI()
	{
		EditorGUILayout.HelpBox(
			"This component drives environment canvas size and unties it " +
			"from Game View size. It is added/removed automatically when " +
			"you open/close prefab and makes no changes to it.",
			MessageType.Info);

		var resizer = (Resizer)target;

		serializedObject.Update();
		EditorGUI.BeginChangeCheck();
		EditorGUILayout.PropertyField(_referenceSize);
		EditorGUILayout.PropertyField(_canvasSizeMode);
		if (EditorGUI.EndChangeCheck())
		{
			// change values in the component instance directly
			// without applying modified properties to serializedObject
			resizer.ReferenceSize = _referenceSize.vector2Value;
			resizer.CanvasSizeMode = _modeValues[_canvasSizeMode.enumValueIndex];

			resizer.SetMode();
		}
	}
}

#endif