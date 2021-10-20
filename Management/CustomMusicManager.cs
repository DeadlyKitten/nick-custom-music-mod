using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using Nick;
using static Nick.MusicMetaData;
using NickCustomMusicMod.Utils;
using System.Threading;

namespace NickCustomMusicMod.Management
{
    internal class CustomMusicManager
	{
		public static string rootCustomSongsPath;

		public static void Init()
        {
			songDictionaries = new Dictionary<string, Dictionary<string, MusicItem>>();

			rootCustomSongsPath = Path.Combine(Paths.BepInExRootPath, "CustomSongs");

			// Create the folder if it doesn't exist
			Directory.CreateDirectory(rootCustomSongsPath);

			Plugin.LogInfo("Loading songs from subfolders...");
			LoadFromSubDirectories(Consts.stagesFolderName);
			LoadFromSubDirectories(Consts.menusFolderName);
			LoadFromSubDirectories(Consts.victoryThemesFolderName);


			Plugin.LogInfo("Generating folders if they don't exist...");
			foreach (string menuID in Consts.MenuIDs)
			{
				Directory.CreateDirectory(Path.Combine(rootCustomSongsPath, Consts.menusFolderName, menuID));
			}

			foreach (string stageName in Consts.StageIDs.Keys)
			{
				Directory.CreateDirectory(Path.Combine(rootCustomSongsPath, Consts.stagesFolderName, stageName));
			}

			foreach (string characterName in Consts.CharacterIDs.Keys)
			{
				Directory.CreateDirectory(Path.Combine(rootCustomSongsPath, Consts.victoryThemesFolderName, characterName));
			}
		}

		public static void LoadFromSubDirectories(string parentFolderName)
		{
			if (!Directory.Exists(Path.Combine(rootCustomSongsPath, parentFolderName))) return;

			var subDirectories = Directory.GetDirectories(Path.Combine(rootCustomSongsPath, parentFolderName));

			Plugin.LogInfo($"Looping through sub directories in \"{parentFolderName}\"");

			// Copy files from old folders to new
			foreach (string directory in subDirectories)
			{
				FileHandlingUtils.UpdateOldFormat(directory);
			}

			// Since we may have deleted folders in the previous step, get the list again
			subDirectories = Directory.GetDirectories(Path.Combine(rootCustomSongsPath, parentFolderName));
			foreach (string directory in subDirectories)
			{
				var folderName = new DirectoryInfo(directory).Name;

				LoadSongsFromFolder(parentFolderName, folderName);
			}
		}

		public static void LoadSongsFromFolder(string parentFolderName, string folderName)
		{
			Plugin.LogInfo($"LoadSongsFromFolder \"{folderName}\"");
			
			string path = Path.Combine(rootCustomSongsPath, parentFolderName, folderName);

			Dictionary<string, MusicItem> musicItemDict = new Dictionary<string, MusicItem>();

			foreach (string text in from x in Directory.GetFiles(path)
									where x.ToLower().EndsWith(".ogg") || x.ToLower().EndsWith(".wav") || x.ToLower().EndsWith(".mp3")
									select x)
			{
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);

				Plugin.LogInfo($"Found custom song: {parentFolderName}\\{folderName}\\{Path.GetFileName(text)}");

				MusicItem music = new MusicItem
				{
					id = "CUSTOM_" + fileNameWithoutExtension,
					originalName = fileNameWithoutExtension,
					resLocation = text,
				};

				if(musicItemDict.ContainsKey(music.id))
				{
					Plugin.LogWarning($"Ignoring \"{text}\" because duplicate file was detected! Do you have two different files with the same name in this folder?");
					continue;
				}

				musicItemDict.Add(music.id, music);
			}

			if (File.Exists(Path.Combine(path, "shared.txt")))
            {
				Plugin.LogDebug($"Found shared file {Path.Combine(folderName, "shared.txt")}");

				var lines = File.ReadAllLines(Path.Combine(path, "shared.txt"));

				foreach (var line in lines)
				{
					var filePath = Path.Combine(rootCustomSongsPath, parentFolderName, "Shared", line);

					if (File.Exists(filePath))
					{
						var music = new MusicItem
						{
							id = "CUSTOM_SHARED_" + Path.GetFileNameWithoutExtension(filePath),
							originalName = Path.GetFileNameWithoutExtension(filePath),
							resLocation = filePath
						};

						if (musicItemDict.ContainsKey(music.id))
						{
							Plugin.LogWarning($"Ignoring \"{filePath}\" because duplicate file was detected! Do you have any duplicates in this file?");
							continue;
						}

						Plugin.LogDebug($"Added shared custom song: {Path.Combine(parentFolderName, folderName, Path.GetFileName(filePath))}");
						musicItemDict.Add(music.id, music);
					}
				}
			}

			string prefix;

			if (parentFolderName == Consts.stagesFolderName || parentFolderName == Consts.menusFolderName)
				prefix = String.Empty;
			else
				prefix = $"{parentFolderName}_";

			songDictionaries.Add(prefix + FileHandlingUtils.TranslateFolderNameToID(folderName), musicItemDict);
		}

		internal static Dictionary<string, Dictionary<string, MusicItem>> songDictionaries;
	}
}
