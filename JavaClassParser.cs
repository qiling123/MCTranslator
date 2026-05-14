using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MCTranslator
{
    public static class JavaClassParser
    {
        // 常量池标签
        private const byte CONSTANT_Utf8 = 1;
        private const byte CONSTANT_String = 8;

        /// <summary>
        /// 从 .class 文件的字节数组中提取所有可能是用户可见文本的字符串字面量
        /// </summary>
        public static List<string> ExtractStringLiterals(byte[] classBytes)
        {
            var result = new List<string>();
            try
            {
                using (var ms = new MemoryStream(classBytes))
                using (var reader = new BinaryReader(ms))
                {
                    // 1. 验证魔数 0xCAFEBABE
                    if (reader.ReadUInt32() != 0xCAFEBABE)
                        return result;

                    // 2. 跳过版本号 (major + minor)
                    reader.ReadUInt32();

                    // 3. 读取常量池大小
                    int cpCount = reader.ReadUInt16();

                    // 存储所有 UTF8 字符串（索引从 1 开始）
                    var utf8Strings = new string[cpCount];

                    // 4. 遍历常量池，收集所有 UTF8 常量
                    for (int i = 1; i < cpCount; i++)
                    {
                        byte tag = reader.ReadByte();
                        switch (tag)
                        {
                            case CONSTANT_Utf8:
                                ushort length = reader.ReadUInt16();
                                byte[] utf8Bytes = reader.ReadBytes(length);
                                utf8Strings[i] = Encoding.UTF8.GetString(utf8Bytes);
                                break;

                            case CONSTANT_String:
                                // String常量指向一个UTF8索引，这里先跳过，后面再处理
                                reader.ReadUInt16();
                                break;

                            case 3: // Integer
                            case 4: // Float
                                reader.ReadInt32();
                                break;

                            case 5: // Long
                            case 6: // Double
                                reader.ReadInt64();
                                i++; // Long/Double 占两个槽位
                                break;

                            case 7: // Class
                            case 9: // Fieldref
                            case 10: // Methodref
                            case 11: // InterfaceMethodref
                            case 12: // NameAndType
                                reader.ReadUInt32();
                                break;

                            case 15: // MethodHandle
                                reader.ReadByte();
                                reader.ReadUInt16();
                                break;

                            case 16: // MethodType
                                reader.ReadUInt16();
                                break;

                            case 17: // Dynamic
                            case 18: // InvokeDynamic
                                reader.ReadUInt16();
                                reader.ReadUInt16();
                                break;

                            default:
                                // 遇到未知标签，跳过该标签的数据部分
                                // 需要根据标签类型跳过对应的字节数，这里保守起见跳过1字节继续
                                reader.ReadByte();
                                break;
                        }
                    }

                    // 5. 从收集到的 UTF8 字符串中过滤出可能是自然语言的条目
                    foreach (var str in utf8Strings)
                    {
                        if (str != null && IsPossiblyVisibleText(str))
                            result.Add(str);
                    }
                }
            }
            catch
            {
                // 如果解析过程中出现任何异常，直接返回已收集的内容
            }
            return result.Distinct().ToList();
        }

        /// <summary>
        /// 启发式判断字符串是否可能是游戏中可见的文本（排除类名、方法签名等）
        /// </summary>
        private static bool IsPossiblyVisibleText(string s)
        {
            // 长度过滤：太短或太长都不像自然语言
            if (s.Length < 2 || s.Length > 500) return false;

            // 必须包含至少一个字母
            if (!s.Any(char.IsLetter)) return false;

            // 排除 Java 类描述符，如 "Lnet/minecraft/block/Block;"
            if (s.Contains('/') && s.Contains(';') && s.StartsWith("L")) return false;

            // 排除方法签名，通常以 '(' 开头
            if (s.StartsWith("(")) return false;

            // 排除常见的纯驼峰类名（大写字母超过一半且没有空格）
            int upperCount = s.Count(char.IsUpper);
            if (upperCount > s.Length / 2 && s.Length > 5 && !s.Contains(' ')) return false;

            // 排除常见的 Java 关键字和布尔值
            string lower = s.ToLowerInvariant();
            string[] blacklist = { "null", "this", "super", "true", "false", "class", "public", "private", "protected", "static", "final", "void" };
            if (blacklist.Contains(lower)) return false;

            // 排除纯路径（包名），包含 '/'' 且没有空格
            if (s.Contains('/') && !s.Contains(' ')) return false;

            return true;
        }
    }
}