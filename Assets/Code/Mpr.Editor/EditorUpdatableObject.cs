using System;
using UnityEditor;
using UnityEngine;

namespace Mpr.AI
{
	/// <summary>
	/// Base class for objects that can be persistently attached to Editor
	/// Windows and will survive play mode changes.
	/// </summary>
	public abstract class EditorUpdatableObject : IDisposable
	{
		Options options;
		bool updating;

		public enum Options
		{
			None = 0,
			UpdateInEditor = 1 << 0,
		}

		public EditorUpdatableObject(Options options = Options.None)
		{
			this.options = options;

			if(options.HasFlag(Options.UpdateInEditor))
			{
				EditorApplication.update += OnUpdate;
				updating = true;
				OnEnable();
			}
			else
			{
				EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;

				if(Application.isPlaying)
				{
					EditorApplication.update += OnUpdate;
					updating = true;
					OnEnable();
				}
			}
		}

		public void Dispose()
		{
			if(options.HasFlag(Options.UpdateInEditor))
			{
				EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
			}

			if(updating)
			{
				updating = false;
				OnDisable();
			}
		}

		private void EditorApplication_playModeStateChanged(PlayModeStateChange stateChange)
		{
			if(stateChange == PlayModeStateChange.EnteredPlayMode)
			{
				EditorApplication.update += OnUpdate;
				updating = true;
				OnEnable();
			}
			else
			{
				EditorApplication.update -= OnUpdate;
				updating = false;
				OnDisable();
			}
		}

		/// <summary>
		/// Invoked when updates start.
		/// </summary>
		protected virtual void OnEnable() { }

		/// <summary>
		/// Invoked once per frame in play mode, and occasionally in edit mode if the updateInEditor flag was passed to the constructor
		/// </summary>
		protected virtual void OnUpdate() { }

		/// <summary>
		/// Invoked when updates end.
		/// </summary>
		protected virtual void OnDisable() { }
	}
}