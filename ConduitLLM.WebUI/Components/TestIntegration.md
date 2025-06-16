# Quick Integration Test

## 1. Add Component Import

Add this to any page you want to test:
```razor
@using ConduitLLM.WebUI.Components
```

## 2. Replace One Modal

Find any modal in your existing pages and replace it. For example, in the Chat Interface or any settings page:

### Before:
```html
<div class="modal show" style="display: block">
    <div class="modal-dialog">
        <div class="modal-content">
            <!-- lots of HTML -->
        </div>
    </div>
</div>
```

### After:
```razor
<Modal @bind-IsVisible="showModal" Title="Settings">
    <BodyContent>
        <!-- Your content -->
    </BodyContent>
    <FooterContent>
        <button class="btn btn-primary" @onclick="Save">Save</button>
    </FooterContent>
</Modal>
```

## 3. Test One Component at a Time

Start small:
1. Replace just one modal first
2. Test it thoroughly
3. If it works, replace similar modals
4. Move on to next component type

## 4. Quick Smoke Test

- Click to open modal
- Verify it displays correctly
- Click backdrop to close (should work)
- Click close button (should work)
- Test your save/cancel actions