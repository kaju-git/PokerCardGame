using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

public class AuthenticationServiceWrapper : MonoBehaviour
{
    public static string PlayerId { get; private set; }
    public static bool IsSignedIn { get; private set; } = false; // サインイン完了を示すフラグ

    async void Start()
    {
        try
        {
            // 既に初期化済でなければ初期化する
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            // 既にサインイン済でなければ匿名でサインインする
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                PlayerId = AuthenticationService.Instance.PlayerId;
                Debug.Log($"Signed in anonymously. Player ID: {PlayerId}");
            }
            else
            {
                PlayerId = AuthenticationService.Instance.PlayerId;
                Debug.Log($"Already signed in. Player ID: {PlayerId}");
            }

            // 全ての処理が成功したら、フラグを立てる
            IsSignedIn = true;
        }
        catch (System.Exception e)
        {
            IsSignedIn = false; // 失敗した場合はフラグを倒す
            Debug.LogError($"Authentication failed: {e.Message}");
        }
    }
}