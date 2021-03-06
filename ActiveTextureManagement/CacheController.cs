﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ActiveTextureManagement
{

    class CacheController
    {
        static String MD5String = "";
        static String LastFile = "";
        static Dictionary<String, TextureInfoWrapper> TextureHashTable = new Dictionary<string, TextureInfoWrapper>();
        static String[] Extensions = { ".png", ".tga", ".mbm", ".jpg", ".jpeg", ".truecolor" };

        public static TextureInfoWrapper FetchCacheTexture(TexInfo Texture, bool compress, bool mipmaps)
        {
            String textureName = Texture.name;
            String originalTextureFile = KSPUtil.ApplicationRootPath + "GameData/" + textureName;
            String cacheFile = KSPUtil.ApplicationRootPath + "GameData/ActiveTextureManagement/textureCache/" + textureName;
            String cacheConfigFile = cacheFile + ".tcache";
            cacheFile += ".imgcache";

            String hashString = GetMD5String(originalTextureFile);
            if (TextureHashTable.ContainsKey(hashString))
            {
                ActiveTextureManagement.DBGLog("hash triggered... " + textureName);
                TextureInfoWrapper tex = TextureHashTable[hashString];
                if (tex.name != textureName)
                {
                    TextureInfoWrapper cacheTexInfo = new TextureInfoWrapper(Texture.file, tex.texture, tex.isNormalMap, tex.isReadable, tex.isCompressed);
                    cacheTexInfo.name = textureName;
                    ActiveTextureManagement.DBGLog("Re-using from hash dictionary... " + textureName+" is a duplicate of "+tex.name);

                    return cacheTexInfo;
                }
            }
            
            if (File.Exists(cacheConfigFile))
            {
                ConfigNode config = ConfigNode.Load(cacheConfigFile);
                string format = config.GetValue("orig_format");
                String cacheHash = config.GetValue("md5");
                int origWidth, origHeight;
                string origWidthString = config.GetValue("orig_width");
                string origHeightString = config.GetValue("orig_height");
                int.TryParse(origWidthString, out origWidth);
                int.TryParse(origHeightString, out origHeight);
               

                if (origWidthString == null || origHeightString == null ||
                    cacheHash == null || format == null)
                {
                    return RebuildCache(Texture, compress, mipmaps, hashString );
                }


                originalTextureFile += format;
                Texture.Resize(origWidth, origHeight);

                if (format != null && File.Exists(originalTextureFile) && File.Exists(cacheFile))
                {
                    
                    String cacheIsNormString = config.GetValue("is_normal");
                    String cacheIsCompressed = config.GetValue("is_compressed");
                    String cacheWidthString = config.GetValue("width");
                    String cacheHeihtString = config.GetValue("height");
                    string hasAlphaString = config.GetValue("hasAlpha");
                    string hasMipmapsString = config.GetValue("hasMipmaps");
                    bool cacheIsNorm = false;
                    int cacheWidth = 0;
                    int cacheHeight = 0;
                    bool hasAlpha = true;
                    bool hasMipmaps = true;
                    bool isCompressed = true;
                    bool.TryParse(cacheIsNormString, out cacheIsNorm);
                    bool.TryParse(cacheIsCompressed, out isCompressed);
                    int.TryParse(cacheWidthString, out cacheWidth);
                    int.TryParse(cacheHeihtString, out cacheHeight);
                    bool.TryParse(hasAlphaString, out hasAlpha);
                    bool.TryParse(hasMipmapsString, out hasMipmaps);

                    if (cacheHash != hashString || compress != isCompressed || mipmaps != hasMipmaps || cacheIsNorm != Texture.isNormalMap || Texture.resizeWidth != cacheWidth || Texture.resizeHeight != cacheHeight)
                    {
                        if (cacheHash != hashString)
                        {
                            ActiveTextureManagement.DBGLog(cacheHash + " != " + hashString);
                        }
                        if (cacheIsNorm != Texture.isNormalMap)
                        {
                            ActiveTextureManagement.DBGLog(cacheIsNorm + " != " + Texture.isNormalMap);
                        }
                        if (Texture.resizeWidth != cacheWidth)
                        {
                            ActiveTextureManagement.DBGLog(Texture.resizeWidth + " != " + cacheWidth);
                        }
                        if (Texture.resizeHeight != cacheHeight)
                        {
                            ActiveTextureManagement.DBGLog(Texture.resizeHeight + " != " + cacheHeight);
                        }
                        return RebuildCache(Texture, compress, mipmaps, hashString);
                    }
                    else
                    {
                        ActiveTextureManagement.DBGLog("Loading from cache... " + textureName);
                        Texture.needsResize = false;
                        Texture.width = Texture.resizeWidth;
                        Texture.height = Texture.resizeHeight;
                        Texture.filename = cacheFile;
                        TextureInfoWrapper tex = TextureConverter.DDSToTexture(Texture.file, Texture, hasMipmaps, isCompressed, hasAlpha);
                        if (TextureHashTable.ContainsKey(hashString))
                        {
                            TextureHashTable[hashString] = tex;
                        }
                        else
                        {
                            TextureHashTable.Add(hashString, tex);
                        }

                        return tex;
                    }
                }
                else
                {
                    return RebuildCache(Texture, compress, mipmaps, hashString);
                }
            }
            else
            {
                return RebuildCache(Texture, compress, mipmaps, hashString);
            }

        }

        private static TextureInfoWrapper RebuildCache(TexInfo Texture, bool compress, bool mipmaps, string hashString)
        {
            Texture.loadOriginalFirst = true;
            ActiveTextureManagement.DBGLog("Loading texture...");
            TextureConverter.GetReadable(Texture, mipmaps);
            ActiveTextureManagement.DBGLog("Texture loaded.");

            TextureInfoWrapper cacheTexture = Texture.texture;
            Texture2D tex = cacheTexture.texture;

            String textureName = cacheTexture.name;
            String cacheFile = KSPUtil.ApplicationRootPath + "GameData/ActiveTextureManagement/textureCache/" + textureName;

            ActiveTextureManagement.DBGLog("Rebuilding Cache... " + Texture.name);

            ActiveTextureManagement.DBGLog("Saving cache file " + cacheFile + ".imgcache");

            Color32[] colors = tex.GetPixels32();
            bool hasAlpha =TextureConverter.WriteTo(tex, cacheFile + ".imgcache", compress);

            String originalTextureFile = Texture.filename;
            String cacheConfigFile = cacheFile + ".tcache";
            ActiveTextureManagement.DBGLog("Created Config for" + originalTextureFile);


            ConfigNode config = new ConfigNode();
            config.AddValue("md5", hashString); ActiveTextureManagement.DBGLog("md5: " + hashString);
            config.AddValue("orig_format", Path.GetExtension(originalTextureFile)); ActiveTextureManagement.DBGLog("orig_format: " + Path.GetExtension(originalTextureFile));
            config.AddValue("orig_width", Texture.width.ToString()); ActiveTextureManagement.DBGLog("orig_width: " + Texture.width.ToString());
            config.AddValue("orig_height", Texture.height.ToString()); ActiveTextureManagement.DBGLog("orig_height: " + Texture.height.ToString());
            config.AddValue("is_normal", cacheTexture.isNormalMap.ToString()); ActiveTextureManagement.DBGLog("is_normal: " + cacheTexture.isNormalMap.ToString());
            config.AddValue("is_compressed", compress); ActiveTextureManagement.DBGLog("is_compressed: " + compress);
            config.AddValue("width", Texture.resizeWidth.ToString()); ActiveTextureManagement.DBGLog("width: " + Texture.resizeWidth.ToString());
            config.AddValue("height", Texture.resizeHeight.ToString()); ActiveTextureManagement.DBGLog("height: " + Texture.resizeHeight.ToString());
            config.AddValue("hasAlpha", hasAlpha); ActiveTextureManagement.DBGLog("hasAlpha: " + hasAlpha.ToString());
            config.AddValue("hasMipmaps", mipmaps); ActiveTextureManagement.DBGLog("hasMipmaps: " + hasAlpha.ToString());
            config.Save(cacheConfigFile);
            ActiveTextureManagement.DBGLog("Saved Config.");

            if (compress)
            {
                tex.Compress(true);
            }
            cacheTexture.isCompressed = compress;
            tex.Apply(false, Texture.makeNotReadable);
            
            cacheTexture.isReadable = !Texture.makeNotReadable;
            
            return cacheTexture;
        }

        static String GetMD5String(String file)
        {
            if(file == LastFile)
            {
                return MD5String;
            }
            MD5String = null;
            foreach (String extension in Extensions)
            {
                if (File.Exists(file + extension))
                {
                    FileStream stream = File.OpenRead(file + extension);
                    MD5 md5 = MD5.Create();
                    byte[] hash = md5.ComputeHash(stream);
                    stream.Close();
                    MD5String = BitConverter.ToString(hash);
                    LastFile = file;
                    return MD5String;
                }
            }
            return MD5String;
        }

        public static int MemorySaved(int originalWidth, int originalHeight, TextureFormat originalFormat, bool originalMipmaps, GameDatabase.TextureInfo Texture)
        {
            int width = Texture.texture.width;
            int height = Texture.texture.height;
            TextureFormat format = Texture.texture.format;
            bool mipmaps = Texture.texture.mipmapCount == 1 ? false : true;
            ActiveTextureManagement.DBGLog("Texture: " + Texture.name);
            ActiveTextureManagement.DBGLog("is normalmap: " + Texture.isNormalMap);
            Texture2D tex = Texture.texture;
            ActiveTextureManagement.DBGLog("originalWidth: " + originalWidth);
            ActiveTextureManagement.DBGLog("originalHeight: " + originalHeight);
            ActiveTextureManagement.DBGLog("originalFormat: " + originalFormat);
            ActiveTextureManagement.DBGLog("originalMipmaps: " + originalMipmaps);
            ActiveTextureManagement.DBGLog("width: " + width);
            ActiveTextureManagement.DBGLog("height: " + height);
            ActiveTextureManagement.DBGLog("format: " + format);
            ActiveTextureManagement.DBGLog("mipmaps: " + mipmaps);
            bool readable = true;
            try { tex.GetPixel(0, 0); }
            catch { readable = false; };
            ActiveTextureManagement.DBGLog("readable: " + readable);
            if (readable != Texture.isReadable)
            { ActiveTextureManagement.DBGLog("Readbility does not match!"); }
            int oldSize = 0;
            int newSize = 0;
            switch (originalFormat)
            {
                case TextureFormat.ARGB32:
                case TextureFormat.RGBA32:
                case TextureFormat.BGRA32:
                    oldSize = 4 * (originalWidth * originalHeight);
                    break;
                case TextureFormat.RGB24:
                    oldSize = 3 * (originalWidth * originalHeight);
                    break;
                case TextureFormat.Alpha8:
                    oldSize = originalWidth * originalHeight;
                    break;
                case TextureFormat.DXT1:
                    oldSize = (originalWidth * originalHeight) / 2;
                    break;
                case TextureFormat.DXT5:
                    oldSize = originalWidth * originalHeight;
                    break;
            }
            switch (format)
            {
                case TextureFormat.ARGB32:
                case TextureFormat.RGBA32:
                case TextureFormat.BGRA32:
                    newSize = 4 * (width * height);
                    break;
                case TextureFormat.RGB24:
                    newSize = 3 * (width * height);
                    break;
                case TextureFormat.Alpha8:
                    newSize = width * height;
                    break;
                case TextureFormat.DXT1:
                    newSize = (width * height) / 2;
                    break;
                case TextureFormat.DXT5:
                    newSize = width * height;
                    break;
            }
            if (originalMipmaps)
            {
                oldSize += (int)(oldSize * .33f);
            }
            if (mipmaps)
            {
                newSize += (int)(newSize * .33f);
            }
            return (oldSize - newSize);
        }
        
    }
}
