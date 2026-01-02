// Multi-step registration wizard
// - draft save/load (server cache) + localStorage fallback
// - step validation
// - email duplicate check
// - curriculum->grade dynamic
// - password strength indicator

(function () {
  var step = 1;
  var draftId = localStorage.getItem("igb_reg_draftId") || ("d_" + Math.random().toString(36).slice(2));
  localStorage.setItem("igb_reg_draftId", draftId);

  var state = {
    accountType: null,
    fullName: "",
    email: "",
    password: "",
    confirmPassword: "",
    timeZoneId: "",
    phone: { countryCode: "+1", number: "" },
    whatsapp: { countryCode: "+1", number: "" },
    student: { dateOfBirth: "", curriculumId: null, gradeId: null, guardian1: {}, guardian2: {} },
    tutor: { dateOfBirth: "", specialities: [], educationHistory: [], workExperience: [] }
  };

  function qs(id) { return document.getElementById(id); }
  function qsa(sel) { return Array.from(document.querySelectorAll(sel)); }

  function setStep(n) {
    step = n;
    qsa(".wizard-step").forEach(function (el) {
      el.classList.toggle("active", Number(el.getAttribute("data-step")) === step);
    });
    qs("prevBtn").disabled = step === 1;
    qs("nextBtn").classList.toggle("d-none", step === 4);
    qs("submitBtn").classList.toggle("d-none", step !== 4);
    qs("stepIndicator").textContent = "Step " + step + " of 4";
    renderRoleSection();
    if (step === 4) renderReview();
  }

  function renderRoleSection() {
    var t = state.accountType;
    qs("studentSection").classList.toggle("d-none", t !== "Student");
    qs("tutorSection").classList.toggle("d-none", t !== "Tutor");
    qs("guardianSection").classList.toggle("d-none", t !== "Guardian");
  }

  function toast(title, message, variant) {
    try { window.igbToast && window.igbToast({ title: title, message: message, variant: variant || "primary" }); } catch { }
  }

  function isEmailValid(e) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(e || "").trim());
  }

  function validateName(name) {
    var n = (name || "").trim();
    if (n.length < 2 || n.length > 50) return "Full name must be 2-50 characters.";
    if (!/^[A-Za-z\s\-']+$/.test(n)) return "Full name must contain letters only.";
    return null;
  }

  function passwordScore(pw) {
    var s = 0;
    if (!pw) return 0;
    if (pw.length >= 8) s += 1;
    if (/[A-Z]/.test(pw)) s += 1;
    if (/[a-z]/.test(pw)) s += 1;
    if (/[0-9]/.test(pw)) s += 1;
    if (/[^a-zA-Z0-9]/.test(pw)) s += 1;
    return s; // 0..5
  }

  function updatePwMeter() {
    var pw = qs("password").value;
    var score = passwordScore(pw);
    var pct = (score / 5) * 100;
    var bar = qs("pwBar");
    bar.style.width = pct + "%";
    bar.style.background = score >= 4 ? "var(--igb-color-success)" : (score >= 3 ? "var(--igb-color-warning)" : "var(--igb-color-danger)");
    qs("pwText").textContent = score >= 4 ? "Strong password." : "Min 8, upper/lower/number/special.";
  }

  async function checkEmailAvailability(email) {
    try {
      var res = await fetch("/api/registration/check-email?email=" + encodeURIComponent(email));
      if (!res.ok) return null;
      return await res.json();
    } catch {
      return null;
    }
  }

  function markInvalid(el, errEl, msg) {
    if (!el) return;
    el.classList.add("is-invalid");
    if (errEl) errEl.textContent = msg || "";
  }
  function clearInvalid(el) {
    if (!el) return;
    el.classList.remove("is-invalid");
  }

  async function validateStep1() {
    var ok = !!state.accountType;
    qs("step1Error").classList.toggle("d-none", ok);
    return ok;
  }

  async function validateStep2() {
    var ok = true;
    qs("submitErr").classList.add("d-none");

    var fullName = qs("fullName");
    clearInvalid(fullName);
    var nameErr = validateName(fullName.value);
    if (nameErr) { markInvalid(fullName, qs("fullNameErr"), nameErr); ok = false; }

    var emailEl = qs("email");
    clearInvalid(emailEl);
    var email = emailEl.value.trim();
    if (!isEmailValid(email)) { markInvalid(emailEl, qs("emailErr"), "Enter a valid email."); ok = false; }
    else {
      qs("emailHint").textContent = "Checking availability...";
      var avail = await checkEmailAvailability(email);
      if (avail && avail.available === false) { markInvalid(emailEl, qs("emailErr"), "Email already exists."); ok = false; }
      qs("emailHint").textContent = "";
    }

    var pwEl = qs("password");
    var pw = pwEl.value;
    var score = passwordScore(pw);
    var pwErr = qs("passwordErr");
    pwErr.style.display = "none";
    if (score < 5) { pwErr.style.display = "block"; pwErr.textContent = "Password must be min 8, upper/lower/number/special."; ok = false; }

    var cpEl = qs("confirmPassword");
    clearInvalid(cpEl);
    if (cpEl.value !== pw) { markInvalid(cpEl, qs("confirmPasswordErr"), "Passwords do not match."); ok = false; }

    var phone = qs("phone").value.trim();
    var phoneErr = qs("phoneErr");
    phoneErr.style.display = "none";
    if (phone.length < 6) { phoneErr.style.display = "block"; phoneErr.textContent = "Phone is required."; ok = false; }

    // profile image validate
    var f = qs("profileImage").files && qs("profileImage").files[0];
    var imgErr = qs("profileImageErr");
    imgErr.style.display = "none";
    if (f) {
      var okType = ["image/jpeg", "image/png"].includes(f.type);
      if (!okType) { imgErr.style.display = "block"; imgErr.textContent = "Profile image must be jpg or png."; ok = false; }
      if (f.size > 5_000_000) { imgErr.style.display = "block"; imgErr.textContent = "Profile image must be <= 5MB."; ok = false; }
    }

    return ok;
  }

  async function validateStep3() {
    var t = state.accountType;
    if (t === "Guardian") return true;

    var ok = true;
    if (t === "Student") {
      clearInvalid(qs("studentDob"));
      if (!qs("studentDob").value) { markInvalid(qs("studentDob"), qs("studentDobErr"), "Date of birth is required."); ok = false; }
      clearInvalid(qs("curriculum"));
      if (!qs("curriculum").value) { markInvalid(qs("curriculum"), qs("curriculumErr"), "Curriculum is required."); ok = false; }
      clearInvalid(qs("grade"));
      if (!qs("grade").value) { markInvalid(qs("grade"), qs("gradeErr"), "Grade is required."); ok = false; }

      clearInvalid(qs("g1Name"));
      if (!qs("g1Name").value.trim()) { markInvalid(qs("g1Name"), qs("g1NameErr"), "Guardian 1 name is required."); ok = false; }
    }

    if (t === "Tutor") {
      clearInvalid(qs("tutorDob"));
      if (!qs("tutorDob").value) { markInvalid(qs("tutorDob"), qs("tutorDobErr"), "Date of birth is required."); ok = false; }

      clearInvalid(qs("specialities"));
      if (!qs("specialities").value.trim()) { markInvalid(qs("specialities"), qs("specialitiesErr"), "Specialities are required."); ok = false; }

      // docs validate (<=10MB each)
      var docs = qs("tutorDocs").files ? Array.from(qs("tutorDocs").files) : [];
      var docsErr = qs("tutorDocsErr");
      docsErr.style.display = "none";
      var tooBig = docs.some(function (d) { return d.size > 10_000_000; });
      if (tooBig) { docsErr.style.display = "block"; docsErr.textContent = "Each document must be <= 10MB."; ok = false; }
    }

    return ok;
  }

  async function validateStep4() {
    var ok = qs("terms").checked;
    qs("termsErr").classList.toggle("d-none", ok);
    return ok;
  }

  function captureStep2() {
    state.fullName = qs("fullName").value.trim();
    state.email = qs("email").value.trim();
    state.password = qs("password").value;
    state.confirmPassword = qs("confirmPassword").value;
    state.phone.countryCode = qs("phoneCc").value;
    state.phone.number = qs("phone").value.trim();
    state.whatsapp.countryCode = qs("waCc").value;
    state.whatsapp.number = qs("whatsapp").value.trim();
    state.timeZoneId = qs("timeZoneId").value.trim();
  }

  function captureStep3() {
    if (state.accountType === "Student") {
      state.student.dateOfBirth = qs("studentDob").value;
      state.student.curriculumId = qs("curriculum").value ? Number(qs("curriculum").value) : null;
      state.student.gradeId = qs("grade").value ? Number(qs("grade").value) : null;
      state.student.guardian1 = { name: qs("g1Name").value.trim(), email: qs("g1Email").value.trim(), phone: qs("g1Phone").value.trim(), relationship: qs("g1Rel").value.trim() };
      state.student.guardian2 = { name: qs("g2Name").value.trim(), email: qs("g2Email").value.trim(), phone: qs("g2Phone").value.trim(), relationship: qs("g2Rel").value.trim() };
    }
    if (state.accountType === "Tutor") {
      state.tutor.dateOfBirth = qs("tutorDob").value;
      state.tutor.specialities = qs("specialities").value.split(",").map(function (s) { return s.trim(); }).filter(Boolean);
      state.tutor.educationHistory = readDynamicList("eduList");
      state.tutor.workExperience = readDynamicList("workList");
    }
  }

  function renderReview() {
    captureStep2();
    captureStep3();
    var lines = [];
    lines.push("<div class='fw-semibold mb-2'>Account</div>");
    lines.push("<div><strong>Type:</strong> " + esc(state.accountType) + "</div>");
    lines.push("<div><strong>Name:</strong> " + esc(state.fullName) + "</div>");
    lines.push("<div><strong>Email:</strong> " + esc(state.email) + "</div>");
    lines.push("<div><strong>Phone:</strong> " + esc(state.phone.countryCode + " " + state.phone.number) + "</div>");
    if (state.whatsapp.number) lines.push("<div><strong>WhatsApp:</strong> " + esc(state.whatsapp.countryCode + " " + state.whatsapp.number) + "</div>");
    if (state.timeZoneId) lines.push("<div><strong>Timezone:</strong> " + esc(state.timeZoneId) + "</div>");

    if (state.accountType === "Student") {
      lines.push("<hr/>");
      lines.push("<div class='fw-semibold mb-2'>Student details</div>");
      lines.push("<div><strong>DOB:</strong> " + esc(state.student.dateOfBirth) + "</div>");
      lines.push("<div><strong>CurriculumId:</strong> " + esc(String(state.student.curriculumId || "")) + "</div>");
      lines.push("<div><strong>GradeId:</strong> " + esc(String(state.student.gradeId || "")) + "</div>");
      lines.push("<div class='mt-2'><strong>Guardian 1:</strong> " + esc(state.student.guardian1.name || "") + "</div>");
    }

    if (state.accountType === "Tutor") {
      lines.push("<hr/>");
      lines.push("<div class='fw-semibold mb-2'>Tutor details</div>");
      lines.push("<div><strong>DOB:</strong> " + esc(state.tutor.dateOfBirth) + "</div>");
      lines.push("<div><strong>Specialities:</strong> " + esc(state.tutor.specialities.join(", ")) + "</div>");
      lines.push("<div class='small muted mt-2'>Tutors require admin approval after email confirmation.</div>");
    }

    qs("reviewBox").innerHTML = lines.join("");
  }

  function esc(s) {
    return String(s || "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll("\"", "&quot;").replaceAll("'", "&#039;");
  }

  async function loadCurricula() {
    try {
      var res = await fetch("/api/registration/curricula");
      var list = await res.json();
      qs("curriculum").innerHTML = "<option value=''>Select</option>" + list.map(function (x) {
        return "<option value='" + x.id + "'>" + esc(x.name) + "</option>";
      }).join("");
    } catch {
      qs("curriculum").innerHTML = "<option value=''>Select</option>";
    }
  }

  async function loadGrades(curriculumId) {
    try {
      var res = await fetch("/api/registration/grades?curriculumId=" + encodeURIComponent(curriculumId));
      var list = await res.json();
      qs("grade").innerHTML = "<option value=''>Select</option>" + list.map(function (x) {
        return "<option value='" + x.id + "'>" + esc(x.name) + "</option>";
      }).join("");
    } catch {
      qs("grade").innerHTML = "<option value=''>Select</option>";
    }
  }

  function addDynamicRow(containerId, defaults) {
    var c = qs(containerId);
    var idx = c.children.length;
    var row = document.createElement("div");
    row.className = "border rounded-3 p-3 mb-2 bg-white";
    row.innerHTML = `
      <div class="row g-2">
        <div class="col-md-4"><input class="form-control" data-k="title" placeholder="Title" value="${esc(defaults?.title||"")}" /></div>
        <div class="col-md-4"><input class="form-control" data-k="org" placeholder="Organization" value="${esc(defaults?.org||"")}" /></div>
        <div class="col-md-3"><input class="form-control" data-k="year" placeholder="Year" value="${esc(defaults?.year||"")}" /></div>
        <div class="col-md-1 d-grid"><button type="button" class="btn btn-outline-danger btn-sm" aria-label="Remove"><i class="fas fa-times"></i></button></div>
      </div>`;
    row.querySelector("button").addEventListener("click", function () { row.remove(); });
    c.appendChild(row);
  }

  function readDynamicList(containerId) {
    var c = qs(containerId);
    return Array.from(c.children).map(function (row) {
      var obj = {};
      row.querySelectorAll("input[data-k]").forEach(function (inp) {
        obj[inp.getAttribute("data-k")] = inp.value.trim();
      });
      return obj;
    }).filter(function (x) { return Object.values(x).some(Boolean); });
  }

  function previewProfileImage() {
    var f = qs("profileImage").files && qs("profileImage").files[0];
    if (!f) return;
    if (!["image/jpeg", "image/png"].includes(f.type)) return;
    var url = URL.createObjectURL(f);
    qs("profilePreview").src = url;
    qs("profilePreview").onload = function () { try { URL.revokeObjectURL(url); } catch { } };
  }

  function detectTimezone() {
    try {
      var tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
      if (tz) qs("timeZoneId").value = tz;
    } catch { }
  }

  function saveLocalDraft() {
    captureStep2();
    captureStep3();
    localStorage.setItem("igb_reg_state", JSON.stringify(state));
  }

  async function saveServerDraft() {
    saveLocalDraft();
    try {
      await fetch("/api/registration/draft", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ draftId: draftId, data: state })
      });
      toast("Draft", "Draft saved.", "success");
    } catch {
      toast("Draft", "Saved locally (offline).", "info");
    }
  }

  async function loadDraft() {
    // server first, then local
    try {
      var res = await fetch("/api/registration/draft/" + encodeURIComponent(draftId));
      if (res.ok) {
        state = await res.json();
        return;
      }
    } catch { }
    try {
      var s = localStorage.getItem("igb_reg_state");
      if (s) state = JSON.parse(s);
    } catch { }
  }

  function hydrate() {
    // step 1
    if (state.accountType) selectAccount(state.accountType);
    // step 2
    qs("fullName").value = state.fullName || "";
    qs("email").value = state.email || "";
    qs("password").value = state.password || "";
    qs("confirmPassword").value = state.confirmPassword || "";
    qs("phoneCc").value = state.phone?.countryCode || "+1";
    qs("phone").value = state.phone?.number || "";
    qs("waCc").value = state.whatsapp?.countryCode || "+1";
    qs("whatsapp").value = state.whatsapp?.number || "";
    qs("timeZoneId").value = state.timeZoneId || qs("timeZoneId").value || "";
    updatePwMeter();
    // student
    qs("studentDob").value = state.student?.dateOfBirth || "";
    // tutor
    qs("tutorDob").value = state.tutor?.dateOfBirth || "";
    qs("specialities").value = (state.tutor?.specialities || []).join(", ");
    // dynamic lists
    qs("eduList").innerHTML = "";
    (state.tutor?.educationHistory || []).forEach(function (x) { addDynamicRow("eduList", x); });
    qs("workList").innerHTML = "";
    (state.tutor?.workExperience || []).forEach(function (x) { addDynamicRow("workList", x); });
  }

  function selectAccount(t) {
    state.accountType = t;
    qsa(".step-card").forEach(function (c) {
      c.classList.toggle("active", c.getAttribute("data-account") === t);
    });
    renderRoleSection();
  }

  async function submit() {
    qs("submitErr").classList.add("d-none");
    qs("submitOk").classList.add("d-none");

    var ok = await validateStep4();
    if (!ok) return;

    captureStep2();
    captureStep3();

    var payload = {
      accountType: state.accountType,
      fullName: state.fullName,
      email: state.email,
      password: state.password,
      timeZoneId: state.timeZoneId,
      phone: { countryCode: state.phone.countryCode, number: state.phone.number },
      whatsapp: { countryCode: state.whatsapp.countryCode, number: state.whatsapp.number },
      student: state.accountType === "Student" ? {
        dateOfBirth: state.student.dateOfBirth,
        curriculumId: state.student.curriculumId,
        gradeId: state.student.gradeId,
        guardian1: state.student.guardian1,
        guardian2: state.student.guardian2 && state.student.guardian2.name ? state.student.guardian2 : null
      } : null,
      tutor: state.accountType === "Tutor" ? {
        dateOfBirth: state.tutor.dateOfBirth,
        specialities: state.tutor.specialities,
        educationHistory: state.tutor.educationHistory,
        workExperience: state.tutor.workExperience
      } : null
    };

    var fd = new FormData();
    fd.append("payload", JSON.stringify(payload));
    var img = qs("profileImage").files && qs("profileImage").files[0];
    if (img) fd.append("profileImage", img);
    var docs = qs("tutorDocs").files ? Array.from(qs("tutorDocs").files) : [];
    docs.forEach(function (d) { fd.append("tutorDocs", d); });

    try {
      var res = await fetch("/api/registration/submit", { method: "POST", body: fd });
      var data = await res.json().catch(function () { return null; });
      if (!res.ok) {
        qs("submitErr").textContent = data?.error || "Registration failed.";
        qs("submitErr").classList.remove("d-none");
        return;
      }

      qs("submitOk").textContent =
        "Registration submitted. " +
        (data.requiresEmailConfirmation ? "Please confirm your email (link sent/logged)." : "Email confirmed.") +
        (data.requiresApproval ? " Tutor accounts require admin approval." : "");
      qs("submitOk").classList.remove("d-none");
      toast("Registered", "Registration submitted successfully.", "success");
      localStorage.removeItem("igb_reg_state");
    } catch {
      qs("submitErr").textContent = "Registration failed (network error).";
      qs("submitErr").classList.remove("d-none");
    }
  }

  // ===== wire events =====
  qsa(".step-card").forEach(function (c) {
    c.addEventListener("click", function () { selectAccount(c.getAttribute("data-account")); });
    c.addEventListener("keydown", function (e) { if (e.key === "Enter" || e.key === " ") selectAccount(c.getAttribute("data-account")); });
  });

  qs("password").addEventListener("input", updatePwMeter);
  qs("profileImage").addEventListener("change", previewProfileImage);
  qs("saveDraftBtn").addEventListener("click", saveServerDraft);

  qs("addEdu").addEventListener("click", function () { addDynamicRow("eduList"); });
  qs("addWork").addEventListener("click", function () { addDynamicRow("workList"); });

  qs("curriculum").addEventListener("change", function () {
    var id = qs("curriculum").value;
    qs("grade").innerHTML = "<option value=''>Select</option>";
    if (id) loadGrades(id);
  });

  qs("prevBtn").addEventListener("click", function () { setStep(Math.max(1, step - 1)); saveLocalDraft(); });
  qs("nextBtn").addEventListener("click", async function () {
    var ok = false;
    if (step === 1) ok = await validateStep1();
    if (step === 2) ok = await validateStep2();
    if (step === 3) ok = await validateStep3();
    if (!ok) return;
    if (step === 2) captureStep2();
    if (step === 3) captureStep3();
    saveLocalDraft();
    setStep(Math.min(4, step + 1));
  });
  qs("submitBtn").addEventListener("click", submit);

  // init
  detectTimezone();
  loadDraft().then(function () {
    hydrate();
    loadCurricula().then(function () {
      if (state.student?.curriculumId) {
        qs("curriculum").value = String(state.student.curriculumId);
        return loadGrades(state.student.curriculumId).then(function () {
          if (state.student?.gradeId) qs("grade").value = String(state.student.gradeId);
        });
      }
    });
  });
})();


