using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : Singleton<SceneLoader>
{
    // 标记是否允许手动激活场景
    private bool isAllowToActivate = false;
    // 缓存当前的异步操作对象
    private AsyncOperation currentAsyncOp;

    // 公共加载方法：外部调用这里传场景名和是否自动加载
    public void LoadScene(string sceneName, bool allowAutoLoad = false)
    {
        // 重置状态
        isAllowToActivate = allowAutoLoad;
        // 启动协程
        StartCoroutine(LoadSceneAsyncCoroutine(sceneName));
    }

    // 协程本体：负责真正的异步加载逻辑
    IEnumerator LoadSceneAsyncCoroutine(string sceneName)
    {
        // 开始异步加载场景
        currentAsyncOp = SceneManager.LoadSceneAsync(sceneName);
        // 先禁止自动激活
        currentAsyncOp.allowSceneActivation = false;

        // 循环监控加载进度
        while (!currentAsyncOp.isDone)
        {

            // 当加载到0.9（资源加载完毕）且允许激活时
            if (currentAsyncOp.progress >= 0.9f && isAllowToActivate)
            {
                currentAsyncOp.allowSceneActivation = true;
            }

            // 意思是“暂停一帧，下一帧再继续循环”
            yield return null;
        }
    }

    // 公共方法：外部手动调用允许场景激活
    public void ActivateSceneManually()
    {
        if (currentAsyncOp != null && currentAsyncOp.progress >= 0.9f)
        {
            isAllowToActivate = true;
        }
    }
}