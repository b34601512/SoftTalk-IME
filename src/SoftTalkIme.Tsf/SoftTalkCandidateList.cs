using System.Runtime.InteropServices;

namespace SoftTalkIme.Tsf;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class SoftTalkCandidateList :
    ITfCandidateListUIElement,
    ITfCandidateListUIElementBehavior
{
    private static readonly Guid ElementGuid = new("D2744CE6-20E2-48D7-9F6C-8E7B3E4F2D19");
    private readonly Func<int, int> _finalizeSelection;
    private readonly Action _abort;
    private IReadOnlyList<string> _items = Array.Empty<string>();
    private nint _documentManager;
    private uint _selection;
    private uint _updatedFlags;
    private bool _shown;

    public SoftTalkCandidateList(Func<int, int> finalizeSelection, Action abort)
    {
        _finalizeSelection = finalizeSelection;
        _abort = abort;
    }

    public void SetItems(IReadOnlyList<string> items)
    {
        _items = items.Take(9).ToArray();
        if (_selection >= _items.Count)
        {
            _selection = 0;
        }
        _updatedFlags = 0x00000002 | 0x00000008 | 0x00000004;
    }

    public void SetDocumentManager(nint documentManager)
    {
        if (_documentManager != 0)
        {
            Marshal.Release(_documentManager);
        }
        _documentManager = documentManager;
    }

    public void SetShown(bool shown)
    {
        _shown = shown;
    }

    public int GetDescription(out string description)
    {
        description = "SoftTalk-IME 话术候选";
        return TsfHResults.SOk;
    }

    public int GetGUID(out Guid elementGuid)
    {
        elementGuid = ElementGuid;
        return TsfHResults.SOk;
    }

    public int Show(int show)
    {
        _shown = show != 0;
        return TsfHResults.SOk;
    }

    public int IsShown(out int show)
    {
        show = _shown ? 1 : 0;
        return TsfHResults.SOk;
    }

    public int GetUpdatedFlags(out uint flags)
    {
        flags = _updatedFlags;
        _updatedFlags = 0;
        return TsfHResults.SOk;
    }

    public int GetDocumentMgr(out nint documentManager)
    {
        documentManager = _documentManager;
        if (documentManager != 0)
        {
            Marshal.AddRef(documentManager);
        }
        return TsfHResults.SOk;
    }

    public int GetCount(out uint count)
    {
        count = (uint)_items.Count;
        return TsfHResults.SOk;
    }

    public int GetSelection(out uint index)
    {
        index = _selection;
        return TsfHResults.SOk;
    }

    public int GetString(uint index, out string text)
    {
        if (index >= _items.Count)
        {
            text = string.Empty;
            return TsfHResults.EInvalidArg;
        }

        text = _items[(int)index];
        return TsfHResults.SOk;
    }

    public int GetPageIndex(uint[]? index, uint size, out uint pageCount)
    {
        pageCount = 1;
        if (size == 0 || index is null || index.Length == 0)
        {
            return TsfHResults.SOk;
        }

        index[0] = 0;
        return TsfHResults.SOk;
    }

    public int SetPageIndex(uint[]? index, uint pageCount)
    {
        return TsfHResults.SOk;
    }

    public int GetCurrentPage(out uint page)
    {
        page = 0;
        return TsfHResults.SOk;
    }

    public int SetSelection(uint index)
    {
        if (index >= _items.Count)
        {
            return TsfHResults.EInvalidArg;
        }

        _selection = index;
        _updatedFlags |= 0x00000004;
        return TsfHResults.SOk;
    }

    public int FinalizeCandidate()
    {
        return _finalizeSelection((int)_selection);
    }

    public int Abort()
    {
        _abort();
        return TsfHResults.SOk;
    }

    public void Dispose()
    {
        if (_documentManager != 0)
        {
            Marshal.Release(_documentManager);
            _documentManager = 0;
        }
    }
}
