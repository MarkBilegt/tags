using CounterStrikeSharp.API.Core;
using TagsApi;
using static TagsApi.Tags;

namespace Tags;

public sealed class TagsAPI : ITagApi
{
    private bool processingTagsUpdatedPre;
    private bool processingTagsUpdatedPost;
    private bool processingMessagePre;
    private bool processingMessage;
    private bool processingMessagePost;

    public event Func<MessageProcess, HookResult>? OnMessageProcessPre;
    public event Func<MessageProcess, HookResult>? OnMessageProcess;
    public event Action<MessageProcess>? OnMessageProcessPost;
    public event Action<CCSPlayerController, Tag>? OnTagsUpdatedPre;
    public event Action<CCSPlayerController, Tag>? OnTagsUpdatedPost;

    public HookResult MessageProcessPre(MessageProcess messageProcess) =>
        InvokeMessageHook(ref processingMessagePre, OnMessageProcessPre, messageProcess);

    public HookResult MessageProcess(MessageProcess messageProcess) =>
        InvokeMessageHook(ref processingMessage, OnMessageProcess, messageProcess);

    public void MessageProcessPost(MessageProcess messageProcess)
    {
        if (processingMessagePost) return;
        processingMessagePost = true;
        try
        {
            OnMessageProcessPost?.Invoke(messageProcess);
        }
        finally
        {
            processingMessagePost = false;
        }
    }

    public void TagsUpdatedPre(CCSPlayerController player, Tag tag)
    {
        if (processingTagsUpdatedPre) return;
        processingTagsUpdatedPre = true;
        try
        {
            OnTagsUpdatedPre?.Invoke(player, tag);
        }
        finally
        {
            processingTagsUpdatedPre = false;
        }
    }

    public void TagsUpdatedPost(CCSPlayerController player, Tag tag)
    {
        if (processingTagsUpdatedPost) return;
        processingTagsUpdatedPost = true;
        try
        {
            OnTagsUpdatedPost?.Invoke(player, tag);
        }
        finally
        {
            processingTagsUpdatedPost = false;
        }
    }

    public void AddAttribute(CCSPlayerController player, TagType types, TagPrePost prePost, string newValue) =>
        Tags.Instance.AddAttribute(player, types, prePost, newValue);

    public void SetAttribute(CCSPlayerController player, TagType types, string newValue) =>
        Tags.Instance.SetAttribute(player, types, newValue);

    public string? GetAttribute(CCSPlayerController player, TagType type) =>
        Tags.Instance.GetAttribute(player, type);

    public void ResetAttribute(CCSPlayerController player, TagType types) =>
        Tags.Instance.ResetAttributes(player);

    public bool GetPlayerChatSound(CCSPlayerController player) => Tags.Instance.GetChatSound(player);

    public void SetPlayerChatSound(CCSPlayerController player, bool value) => Tags.Instance.SetChatSound(player, value);

    public bool GetPlayerVisibility(CCSPlayerController player) => Tags.Instance.GetVisibility(player);

    public void SetPlayerVisibility(CCSPlayerController player, bool value) => Tags.Instance.SetVisibility(player, value);

    public void ReloadTags() => Tags.Instance.ReloadTags();

    private static HookResult InvokeMessageHook(
        ref bool processing,
        Func<MessageProcess, HookResult>? hook,
        MessageProcess messageProcess)
    {
        if (processing) return HookResult.Continue;
        processing = true;
        try
        {
            return hook?.Invoke(messageProcess) ?? HookResult.Continue;
        }
        finally
        {
            processing = false;
        }
    }
}
