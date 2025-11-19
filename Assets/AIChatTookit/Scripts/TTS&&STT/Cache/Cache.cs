using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 缓存类
public class Cache
{
    #region 缓存声明

    // 缓存实例
    public static Cache instance = null;

    // 玩家缓存
    public static PlayerCache player = null;

    // 克隆训练
    //public static TrainCache train = null;

    

    #endregion

    // 是否已初始化标志
    private static bool isInit = false;

    /// <summary>
    /// 初始化方法
    /// </summary>
    public static void Init()
    {
        if (isInit) return;
        instance = new Cache();
        player = new PlayerCache();
        

        isInit = true;
    }

    /// <summary>
    /// 退出登录初始化方法
    /// </summary>
    public static void Reset()
    {
        instance = new Cache();
        player = new PlayerCache();
        
    }
}
