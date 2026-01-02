// Auto preview selected image files for inputs rendered by Components/Forms/_FileUploadImage.cshtml
(function () {
  function onChange(e) {
    var input = e.target;
    if (!input || input.type !== "file") return;

    var previewId = input.getAttribute("data-preview-img-id");
    if (!previewId) return;

    var img = document.getElementById(previewId);
    if (!img) return;

    var file = input.files && input.files[0];
    if (!file) return;
    if (!file.type || !file.type.startsWith("image/")) return;

    var url = URL.createObjectURL(file);
    img.src = url;
    img.onload = function () {
      try { URL.revokeObjectURL(url); } catch { /* ignore */ }
    };
  }

  document.addEventListener("change", function (e) {
    if (e.target && e.target.classList && e.target.classList.contains("igb-file-upload")) {
      onChange(e);
    }
  });
})();


