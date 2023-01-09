﻿namespace BitMono.GUI.Shared
{
    public partial class CreateProtectionModal
    {
        [Inject] public IJSRuntime JSRuntime { get; set; }
        [Parameter, EditorRequired] public ICollection<ProtectionSettings> Collection { get; set; }
        [Parameter] public EventCallback<ProtectionSettings> OnSubmit { get; set; }
        public ProtectionSettings Model { get; set; }


        protected override Task OnInitializedAsync()
        {
            Model = new ProtectionSettings { Name = string.Empty };
            return Task.CompletedTask;
        }


        public async Task ShowAsync()
        {
            await JSRuntime.ShowModalStaticAsync(nameof(CreateProtectionModal));
        }
        private bool isLoading = false;
        public async Task SubmitAsync()
        {
            isLoading = true;
            await OnSubmit.InvokeAsync(Model);
            await JSRuntime.HideModalAsync(nameof(CreateProtectionModal));
            isLoading = false;
        }
    }
}