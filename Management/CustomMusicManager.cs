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
			// Load songs
			LoadFromSubDirectories(Consts.stagesFolderName);
			LoadFromSubDirectories(Consts.menusFolderName);
			LoadFromSubDirectories(Consts.victoryThemesFolderName);


			Plugin.LogInfo("Generating folders if they don't exist...");
			// Generate folders, incase any don't exist 
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

            Directory.CreateDirectory(Path.Combine(rootCustomSongsPath, Consts.musicBankFolderName));
		}

		public static void LoadFromSubDirectories(string parentFolderName)
		{
			if (!Directory.Exists(Path.Combine(rootCustomSongsPath, parentFolderName))) return;

			var subDirectories = Directory.GetDirectories(Path.Combine(rootCustomSongsPath, parentFolderName));

			Plugin.LogInfo($"Looping through sub directories in \"{parentFolderName}\"");

			// Copy files from old folders to new
			foreach (string directory in subDirectories)
			{
				UpdateOldFormat(directory);
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
				where x.ToLower().EndsWith(".ogg") || x.ToLower().EndsWith(".wav") || x.ToLower().EndsWith(".mp3") || x.ToLower().EndsWith(".txt")
				select x)
				{
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);

				Plugin.LogInfo($"Found custom song: {parentFolderName}\\{folderName}\\{Path.GetFileName(text)}");

                MusicItem music = new MusicItem {
                    id = "CUSTOM_" + fileNameWithoutExtension,
                    originalName = fileNameWithoutExtension,
                    resLocation = text,
                };
                // Files with a .txt extension will be redirected to the Music Bank folder to find music of the same name, with a naming convention of example.ogg.txt
                if (Path.GetExtension(text) == ".txt") {
                    music.resLocation = Path.Combine(rootCustomSongsPath, Consts.musicBankFolderName, fileNameWithoutExtension).ToString();
                    if (File.Exists(music.resLocation)) 
                        musicItemDict.Add(music.id, music);
                    // As to not double up and not add non existent files
                    continue;
                }

				musicItemDict.Add(music.id, music);
			}

			string prefix;

			if (parentFolderName == Consts.stagesFolderName || parentFolderName == Consts.menusFolderName)
				prefix = String.Empty;
			else
				prefix = $"{parentFolderName}_";

			songDictionaries.Add(prefix + TranslateFolderNameToID(folderName), musicItemDict);
		}

		public static string TranslateFolderNameToID(string folderName) {
			if (Consts.StageIDs.ContainsKey(folderName))
			{
				return Consts.StageIDs[folderName];
			} else if (Consts.CharacterIDs.ContainsKey(folderName))
            {
				return Consts.CharacterIDs[folderName];
            }
			return folderName;
		}

		public static void UpdateOldFormat(string folderPath) {
			var folderName = Path.GetFileName(folderPath);

			if (Consts.StageIDs.ContainsValue(folderName))
			{
				string updatedStageName = "";
				foreach (string key in Consts.StageIDs.Keys)
				{
					if (Consts.StageIDs[key] == folderName) {
						updatedStageName = key;
                    }
				}

				// StageID and display name are the same. EX: Omashu
				if (folderName.Equals(updatedStageName)) {
					return;
                }
				
				string updatedFolderPath = Path.Combine(Directory.GetParent(folderPath).FullName, updatedStageName);

				try {
					Plugin.LogInfo($"Renaming \"{folderName}\" to \"{updatedStageName}\"...");
					Directory.Move(folderPath, updatedFolderPath);
				} catch (IOException ex){
					Plugin.LogInfo($"Could not rename directory \"{folderName}\"! Maybe the new directory already exists?");
					Plugin.LogInfo($"Attempting to copy files from \"{folderName}\" to \"{updatedStageName}\" instead.");
					CopyFilesAndDeleteOriginalFolder(folderPath, updatedFolderPath);
				}
				catch (Exception ex)
				{
					Plugin.LogError($"Failed to rename old folder \"{folderName}\" to \"{updatedStageName}\"!");
					Plugin.LogError($"Exception {ex.Message}");
				}
			}
		}

		public static bool CopyFilesAndDeleteOriginalFolder(string originalDirPath, string targetDirPath) {
			string[] files = Directory.GetFiles(originalDirPath);

			try {
				// Copy the files and overwrite destination files if they already exist.
				foreach (string filePath in files)
				{
					string fileName = Path.GetFileName(filePath);
					string destPath = Path.Combine(targetDirPath, fileName);
					Plugin.LogInfo($"Copying file \"{fileName}\" to \"{destPath}\"");
					File.Copy(filePath, destPath, true);
				}

				Plugin.LogInfo($"Finished copying files. Deleting original folder \"{originalDirPath}\"");

				try {
					// Delete og folder after copying files
					Directory.Delete(originalDirPath, true);
					Plugin.LogInfo($"Deleted \"{originalDirPath}\"");
				} catch(Exception ex) {
					Plugin.LogError($"Failed to delete original folder \"{originalDirPath}\"!");
					Plugin.LogError($"Exception {ex.Message}");
					return false;
				}
			} catch(Exception ex) {
				Plugin.LogError($"Failed to copy files from folder \"{originalDirPath}\" to \"{targetDirPath}\"!");
				Plugin.LogError($"Exception {ex.Message}");
				return false;
			}

			return true;
		}

		internal static Dictionary<string, Dictionary<string, MusicItem>> songDictionaries;
	}
}
