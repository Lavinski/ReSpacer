﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using Enexure.SolutionSettings.Settings.Version1;
using Enexure.SolutionSettings.Settings.Version2;
using Newtonsoft.Json;
using TabSettings = Enexure.SolutionSettings.Settings.Version2.TabSettings;

namespace Enexure.SolutionSettings.Services
{
	class SettingsPersister
	{
		private static readonly IDictionary<int, Func<StreamReader, Task<VisualStudioSettings>>> versionReader;

		static SettingsPersister()
		{
			versionReader = new Dictionary<int, Func<StreamReader, Task<VisualStudioSettings>>>() {
				{ 2, LoadVersion2Async }
			};
		}

		public static async Task SaveAsync(string path, VisualStudioSettings settings)
		{
			var data = JsonConvert.SerializeObject(settings, Formatting.Indented);

			using (var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
			using (var writer = new StreamWriter(file)) {

				await writer.WriteAsync(data);
			}
		}

		public static async Task<VisualStudioSettings> LoadAsync(string path)
		{
			try {
				using (var file = new FileStream(path, FileMode.Open, FileAccess.Read))
				using (var reader = new StreamReader(file)) {

					var versionNumber = await GetValueAsync(reader);
					if (versionNumber > 1) {

						return await versionReader[versionNumber](reader);
					}

					file.Seek(0, SeekOrigin.Begin);
					reader.DiscardBufferedData();
					return await LoadVersion1Async(reader);
				}
			} catch (FileNotFoundException ex) {
				return null;
			}
		}

		private static async Task<int> GetValueAsync(StreamReader reader)
		{
			const int bufferSize = 4;
			var buffer = new char[bufferSize];

			await reader.ReadAsync(buffer, 0, bufferSize);

			var version = new String(buffer);

			int versionNumber;
			if (!int.TryParse(version, out versionNumber)) {
				versionNumber = 1;
			}

			return versionNumber;
		}

		private static async Task<VisualStudioSettings> LoadVersion1Async(StreamReader reader)
		{
			//var text = await reader.ReadToEndAsync();

			var serializer = new JsonSerializer();
			return await Task.Factory.StartNew(() => {
				using (var jsonTextReader = new JsonTextReader(reader)) {
					var data = serializer.Deserialize<ItemSetting[]>(jsonTextReader);

					var settings = new VisualStudioSettings();
					foreach (var item in data) {
						settings.Add(new SettingsPropertyCollection() {
							Name = item.Name,
							Settings = new TextEditorSettings() {
								TabSettings = new TabSettings() {
									IndentSize = item.Settings.IndentSize,
									IndentStyle = item.Settings.IndentStyle,
									InsertTabs = item.Settings.InsertTabs,
								}
							}
						});
					}

					return settings;
				}
			});
		}

		private static async Task<VisualStudioSettings> LoadVersion2Async(StreamReader reader)
		{
			var serializer = new JsonSerializer();
			return await Task.Factory.StartNew(() => {
				using (var jsonTextReader = new JsonTextReader(reader)) {
					return serializer.Deserialize<VisualStudioSettings>(jsonTextReader);
				}
			});
		}
	}
}
