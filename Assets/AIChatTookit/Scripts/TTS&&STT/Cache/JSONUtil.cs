using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// json操作工具
/// </summary>
public sealed class JSONUtil
{

    public static bool readBool(Dictionary<string, object> jsonmap, string key)
    {
        object value = null;
        if (jsonmap.TryGetValue(key, out value))
        {
            return Convert.ToBoolean(value);
        }
        else
        {
            return false;
        }
    }


    /// <summary>
    /// 读取boolean类型值
    /// </summary>
    /// <param name="jsonmap">json集合</param>
    /// <param name="key">访问集合的key值</param>
    /// <returns></returns>
    public static bool readBoolean(Dictionary<string, object> jsonmap, params string[] keys)
    {
        return readBoolean(false, jsonmap, keys);
    }
    /// <summary>
    ///  读取boolean类型值
    /// </summary>
    /// <param name="defaultValue">默认值</param>
    /// <param name="jsonmap">json</param>
    /// <param name="keys">访问集合的key值</param>
    /// <returns></returns>
    public static bool readBoolean(bool defaultValue, Dictionary<string, object> jsonmap, params string[] keys)
    {
        if (jsonmap == null) return defaultValue;
        Dictionary<string, object> temp = jsonmap;
        foreach (string key in keys)
        {
            if (temp.ContainsKey(key))
            {
                object vlaue = temp[key];
                if (vlaue != null)
                {
                    if (vlaue is Dictionary<string, object>)
                    {
                        temp = (Dictionary<string, object>)vlaue;
                    }
                    else
                    {
                        return (bool)vlaue;
                    }
                }

            }
            else
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// 读取string类型值
    /// </summary>
    /// <param name="jsonmap">json集合</param>
    /// <param name="key">访问集合的key值</param>
    /// <returns></returns>
    public static string readString(Dictionary<string, object> jsonmap, params string[] keys)
    {
        return readString(null, jsonmap, keys);
    }
    /// <summary>
    ///  读取boolean类型值
    /// </summary>
    /// <param name="defaultValue">默认值</param>
    /// <param name="jsonmap">json</param>
    /// <param name="keys">访问集合的key值</param>
    /// <returns></returns>
    public static string readString(string defaultValue, Dictionary<string, object> jsonmap, params string[] keys)
    {
        if (jsonmap == null) return defaultValue;
        Dictionary<string, object> temp = jsonmap;
        foreach (string key in keys)
        {
            if (temp.ContainsKey(key))
            {
                object vlaue = temp[key];
                if (vlaue != null)
                {
                    if (vlaue is Dictionary<string, object>)
                    {
                        temp = (Dictionary<string, object>)vlaue;
                    }
                    else
                    {
                        return Convert.ToString(vlaue); ;
                    }

                }
                else
                {
                    return defaultValue;
                }
            }
            else
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// 读取boolean类型值
    /// </summary>
    /// <param name="jsonmap">json集合</param>
    /// <param name="key">访问集合的key值</param>
    /// <returns></returns>
    public static int readInt(Dictionary<string, object> jsonmap, params string[] keys)
    {
        return readInt(0, jsonmap, keys);
    }
    /// <summary>
    ///  读取int类型值
    /// </summary>
    /// <param name="defaultValue">默认值</param>
    /// <param name="jsonmap">json</param>
    /// <param name="keys">访问集合的key值</param>
    /// <returns></returns>
    public static int readInt(int defaultValue, Dictionary<string, object> jsonmap, params string[] keys)
    {
        if (jsonmap == null) return defaultValue;
        Dictionary<string, object> temp = jsonmap;
        foreach (string key in keys)
        {
            if (temp.ContainsKey(key))
            {
                object vlaue = temp[key];
                if (vlaue != null)
                {
                    if (vlaue is Dictionary<string, object>)
                    {
                        temp = (Dictionary<string, object>)vlaue;
                    }
                    else
                    {
                        return Convert.ToInt32(vlaue);
                    }
                }
                else
                {
                    return defaultValue;
                }
            }
            else
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }


    /// <summary>
    /// </summary>
    /// <param name="jsonmap">json集合</param>
    /// <param name="key">访问集合的key值</param>
    /// <returns></returns>
    public static long readLong(Dictionary<string, object> jsonmap, params string[] keys)
    {
        return readLong(0, jsonmap, keys);
    }
    /// <summary>
    ///  读取long类型值
    /// </summary>
    /// <param name="defaultValue">默认值</param>
    /// <param name="jsonmap">json</param>
    /// <param name="keys">访问集合的key值</param>
    /// <returns></returns>
    public static long readLong(int defaultValue, Dictionary<string, object> jsonmap, params string[] keys)
    {
        if (jsonmap == null) return defaultValue;
        Dictionary<string, object> temp = jsonmap;
        foreach (string key in keys)
        {
            if (temp.ContainsKey(key))
            {
                object vlaue = temp[key];
                if (vlaue != null)
                {
                    if (vlaue is Dictionary<string, object>)
                    {
                        temp = (Dictionary<string, object>)vlaue;
                    }
                    else
                    {
                        return Convert.ToInt64(vlaue);
                    }
                }
                else
                {
                    return defaultValue;
                }
            }
            else
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }


    /// <summary>
    /// 读取boolean类型值
    /// </summary>
    /// <param name="jsonmap">json集合</param>
    /// <param name="key">访问集合的key值</param>
    /// <returns></returns>
    public static float readFloat(Dictionary<string, object> jsonmap, params string[] keys)
    {
        return readFloat(0, jsonmap, keys);
    }
    /// <summary>
    ///  读取int类型值
    /// </summary>
    /// <param name="defaultValue">默认值</param>
    /// <param name="jsonmap">json</param>
    /// <param name="keys">访问集合的key值</param>
    /// <returns></returns>
    public static float readFloat(int defaultValue, Dictionary<string, object> jsonmap, params string[] keys)
    {
        if (jsonmap == null) return defaultValue;
        Dictionary<string, object> temp = jsonmap;
        foreach (string key in keys)
        {
            if (temp.ContainsKey(key))
            {
                object vlaue = temp[key];
                if (vlaue != null)
                {
                    if (vlaue is Dictionary<string, object>)
                    {
                        temp = (Dictionary<string, object>)vlaue;
                    }
                    else
                    {
                        return Convert.ToSingle(vlaue);// float.Parse(vlaue.ToString());
                    }
                }
                else
                {
                    return defaultValue;
                }
            }
            else
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// 读取boolean类型值
    /// </summary>
    /// <param name="jsonmap">json集合</param>
    /// <param name="key">访问集合的key值</param>
    /// <returns></returns>
    public static List<object> readList(Dictionary<string, object> jsonmap, params string[] keys)
    {
        return readList(null, jsonmap, keys);
    }

    public static List<string> readListString(Dictionary<string, object> jsonmap, string key)
    {
        object value = null;
        List<string> iList = null;
        if (jsonmap.TryGetValue(key, out value))
        {
            List<object> v = value as List<object>;
            iList = new List<string>();
            for (int i = 0; i < v.Count; i++)
            {
                string iv = Convert.ToString(v[i]);
                iList.Add(iv);
            }
            return iList;
        }
        else
        {
            return null;
        }
    }
    public static List<float> readListFloat(Dictionary<string, object> jsonmap, string key)
    {
        object value = null;
        List<float> iList = null;
        if (jsonmap.TryGetValue(key, out value))
        {
            List<object> v = value as List<object>;
            iList = new List<float>();
            for (int i = 0; i < v.Count; i++)
            {
                int iv = Convert.ToInt32(v[i]);
                iList.Add(iv);
            }
            return iList;
        }
        else
        {
            return null;
        }
    }

    public static List<int> readListInt(Dictionary<string, object> jsonmap, string key)
    {
        object value = null;
        List<int> iList = null;
        if (jsonmap.TryGetValue(key, out value))
        {
            List<object> v = value as List<object>;
            iList = new List<int>();
            for (int i = 0; i < v.Count; i++)
            {
                int iv = Convert.ToInt32(v[i]);
                iList.Add(iv);
            }
            return iList;
        }
        else
        {
            return null;
        }
    }
    /// <summary>
    ///  读取int类型值
    /// </summary>
    /// <param name="defaultValue">默认值</param>
    /// <param name="jsonmap">json</param>
    /// <param name="keys">访问集合的key值</param>
    /// <returns></returns>
    public static List<object> readList(List<object> defaultValue, Dictionary<string, object> jsonmap, params string[] keys)
    {
        if (jsonmap == null) return defaultValue;
        Dictionary<string, object> temp = jsonmap;
        foreach (string key in keys)
        {
            if (temp.ContainsKey(key))
            {
                object vlaue = temp[key];
                if (vlaue != null)
                {
                    if (vlaue is Dictionary<string, object>)
                    {
                        temp = (Dictionary<string, object>)vlaue;
                    }
                    else
                    {
                        return (List<object>)vlaue;
                    }
                }
                else
                {
                    return defaultValue;
                }
            }
            else
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }
    /// <summary>
    /// 读取boolean类型值
    /// </summary>
    /// <param name="jsonmap">json集合</param>
    /// <param name="key">访问集合的key值</param>
    /// <returns></returns>
    public static Dictionary<string, object> readDictionary(Dictionary<string, object> jsonmap, params string[] keys)
    {
        return readDictionary(null, jsonmap, keys);
    }
    /// <summary>
    ///  读取boolean类型值
    /// </summary>
    /// <param name="defaultValue">默认值</param>
    /// <param name="jsonmap">json</param>
    /// <param name="keys">访问集合的key值</param>
    /// <returns></returns>
    public static Dictionary<string, object> readDictionary(Dictionary<string, object> defaultValue, Dictionary<string, object> jsonmap, params string[] keys)
    {
        if (jsonmap == null) return defaultValue;
        Dictionary<string, object> temp = jsonmap;
        foreach (string key in keys)
        {
            if (temp.ContainsKey(key))
            {
                object vlaue = temp[key];
                if (vlaue != null)
                {
                    if (vlaue is Dictionary<string, object>)
                    {
                        temp = (Dictionary<string, object>)vlaue;
                        return temp;
                    }
                }

            }
            else
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    public static Dictionary<string, object> readDictionary(List<object> list, int index)
    {
        if (list == null || list.Count == 0) return null;
        return list[index] as Dictionary<string, object>;
    }
}
