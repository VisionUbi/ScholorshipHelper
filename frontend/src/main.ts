import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Component, Injectable, inject } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { CanActivateFn, Router, RouterLink, RouterOutlet, provideRouter } from '@angular/router';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { BehaviorSubject, Observable, catchError, throwError } from 'rxjs';
import { HttpInterceptorFn } from '@angular/common/http';

const API = 'https://localhost:7139/api';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const token = localStorage.getItem('token');
  const authReq = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  return next(authReq).pipe(catchError(error => {
    if (error.status === 401) {
      localStorage.removeItem('token');
      router.navigateByUrl('/login');
    }
    return throwError(() => error);
  }));
};

const authGuard: CanActivateFn = () => {
  return true;
};

@Injectable({ providedIn: 'root' })
class ApiService {
  private http = inject(HttpClient);

  login(body: unknown) {
    localStorage.setItem('token', 'backend-v2-dev');
    return new Observable<any>(observer => {
      observer.next({ token: 'backend-v2-dev' });
      observer.complete();
    });
  }

  get<T>(path: string): Observable<T> {
    return this.http.get<T>(`${API}${path}`);
  }

  post<T>(path: string, body: unknown): Observable<T> {
    return this.http.post<T>(`${API}${path}`, body);
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<T>(`${API}${path}`, body);
  }
}

@Injectable({ providedIn: 'root' })
class ProfileStore {
  private api = inject(ApiService);
  private profileSubject = new BehaviorSubject<any>(null);
  profile$ = this.profileSubject.asObservable();

  get current() {
    return this.profileSubject.value;
  }

  load() {
    this.api.get<any>('/profile').subscribe(profile => this.profileSubject.next(profile));
  }

  set(profile: any) {
    this.profileSubject.next(profile);
  }
}

@Component({
  standalone: true,
  selector: 'app-root',
  imports: [RouterLink, RouterOutlet],
  template: `
    <aside>
      <h1>Scholarship AutoFill</h1>
      <a routerLink="/">Dashboard</a>
      <a routerLink="/profile">Profile</a>
      <a routerLink="/documents">Documents</a>
      <a routerLink="/tracker">Tracker</a>
      <a routerLink="/analyzer">Scholarship AI</a>
      <a routerLink="/automation">Automation</a>
      <a routerLink="/review">Review</a>
      <a routerLink="/writer">SOP Writer</a>
      <a routerLink="/login">Login</a>
    </aside>
    <main><router-outlet /></main>
  `
})
class AppComponent {
  private profileStore = inject(ProfileStore);

  constructor() {
    this.profileStore.load();
  }
}

@Component({
  standalone: true,
  imports: [FormsModule, ReactiveFormsModule],
  template: `
    <section class="panel narrow">
      <h2>Login</h2>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label>Email <input formControlName="email"></label>
        <label>Password <input type="password" formControlName="password"></label>
        <button>Login</button>
      </form>
<p>Seed login: ubaidkhank1998&#64;gmail.com / ChangeMe123!</p>
    </section>
  `
})
class LoginComponent {
  private fb = inject(FormBuilder);
  private api = inject(ApiService);
  private router = inject(Router);
  form = this.fb.group({ email: ['ubaidkhank1998@gmail.com', Validators.required], password: ['ChangeMe123!', Validators.required] });
  submit() { this.api.login(this.form.value).subscribe(() => this.router.navigateByUrl('/')); }
}

@Component({
  standalone: true,
  imports: [DatePipe],
  template: `
    <section class="grid">
      <article><strong>Profile</strong><span>Saved applicant details and IELTS data</span></article>
      <article><strong>Documents</strong><span>Vault for CV, passport, transcripts, SOPs</span></article>
      <article><strong>Applications</strong><span>Track universities, scholarships, deadlines</span></article>
      <article><strong>Automation Rule</strong><span>Final submit is always manual</span></article>
    </section>
  `
})
class DashboardComponent {}

@Component({
  standalone: true,
  imports: [FormsModule, ReactiveFormsModule],
  template: `
    <section class="panel">
      <h2>Applicant Profile</h2>
      <form [formGroup]="form" (ngSubmit)="save()" class="form-grid">
        <label>Full name <input formControlName="fullName"></label>
        <label>Date of birth <input formControlName="dateOfBirth"></label>
        <label>Nationality <input formControlName="nationality"></label>
        <label>Gender <input formControlName="gender"></label>
        <label>Passport <input formControlName="passportNumber"></label>
        <label>CNIC <input formControlName="nationalId"></label>
        <label>Email <input formControlName="email"></label>
        <label>Phone <input formControlName="phone"></label>
        <label class="wide">Address <textarea formControlName="address"></textarea></label>
        <label>Highest degree <input formControlName="highestDegree"></label>
        <label>University <input formControlName="university"></label>
        <label>CGPA <input formControlName="cgpa"></label>
        <label>Graduation year <input formControlName="graduationYear"></label>
        <label>IELTS overall <input formControlName="ieltsOverall"></label>
        <label>CEFR <input formControlName="cefrLevel"></label>
        <label class="wide">Skills <textarea formControlName="skills"></textarea></label>
        <label class="wide">Research interests <textarea formControlName="researchInterests"></textarea></label>
        <label class="wide">Scholarship preferences <textarea formControlName="scholarshipPreferences"></textarea></label>
        <label class="wide">Preferred regions <textarea formControlName="preferredRegions"></textarea></label>
        <button>Save Profile</button>
      </form>
    </section>
  `
})
class ProfileComponent {
  private fb = inject(FormBuilder);
  private api = inject(ApiService);
  private profileStore = inject(ProfileStore);
  form = this.fb.group({
    id: [''], userId: [''], fullName: [''], dateOfBirth: [''], nationality: [''], gender: [''], passportNumber: [''], nationalId: [''],
    placeOfBirth: [''], email: [''], phone: [''], address: [''], highestDegree: [''], university: [''], cgpa: [''], graduationYear: [''],
    ieltsOverall: [''], ieltsListening: [''], ieltsReading: [''], ieltsWriting: [''], ieltsSpeaking: [''], cefrLevel: [''],
    experienceSummary: [''], skills: [''], researchInterests: [''], scholarshipPreferences: [''], preferredRegions: ['']
  });
  constructor() { this.profileStore.profile$.subscribe(p => p && this.form.patchValue(p)); }
  save() { this.api.put<any>('/profile', this.form.value).subscribe(p => this.profileStore.set(p)); }
}

@Component({
  standalone: true,
  template: `
    <section class="panel">
      <h2>Document Vault</h2>
      <div class="upload">
        <input type="file" (change)="file = $any($event.target).files[0]">
        <input placeholder="Category" #category>
        <input placeholder="Notes" #notes>
        <button (click)="upload(category.value, notes.value)">Upload</button>
      </div>
      <table><tr><th>Name</th><th>Category</th><th>Uploaded</th></tr>
        @for (doc of docs; track doc.id) { <tr><td>{{doc.originalFileName}}</td><td>{{doc.category}}</td><td>{{doc.createdAtUtc | date}}</td></tr> }
      </table>
    </section>
  `
})
class DocumentsComponent {
  private http = inject(HttpClient);
  docs: any[] = [];
  file?: File;
  constructor() { this.load(); }
  load() { this.http.get<any>(`${API}/documents/status`).subscribe(x => this.docs = [{ id: 'status', originalFileName: x.status, category: x.message, createdAtUtc: new Date() }]); }
  upload(category: string, notes: string) {
    this.docs = [{ id: 'chunk1', originalFileName: 'Document upload comes after Chunk 1', category: 'Not implemented yet', createdAtUtc: new Date() }];
  }
}

@Component({
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <section class="panel">
      <h2>Scholarship Tracker</h2>
      <form [formGroup]="form" (ngSubmit)="create()" class="form-grid">
        <input formControlName="universityName" placeholder="University">
        <input formControlName="country" placeholder="Country">
        <input formControlName="programName" placeholder="Program">
        <input formControlName="degreeLevel" placeholder="Degree level">
        <input formControlName="scholarshipName" placeholder="Scholarship">
        <input formControlName="applicationLink" placeholder="Application link">
        <input formControlName="deadline" placeholder="Deadline YYYY-MM-DD">
        <select formControlName="priority"><option>High</option><option>Medium</option><option>Low</option></select>
        <button>Add Application</button>
      </form>
      <table><tr><th>University</th><th>Program</th><th>Status</th><th>Deadline</th><th>Submit</th></tr>
        @for (app of apps; track app.id) { <tr><td>{{app.universityName}}</td><td>{{app.programName}}</td><td>{{app.status}}</td><td>{{app.deadline}}</td><td>{{app.finalSubmitted ? 'Submitted' : 'Manual only'}}</td></tr> }
      </table>
    </section>
  `
})
class TrackerComponent {
  private fb = inject(FormBuilder);
  private api = inject(ApiService);
  apps: any[] = [];
  form = this.fb.group({ universityName: [''], country: [''], programName: [''], degreeLevel: ['Masters'], scholarshipName: [''], applicationLink: [''], deadline: [''], priority: ['Medium'] });
  constructor() { this.load(); }
  load() { this.apps = []; }
  create() { this.apps = [{ ...this.form.value, id: crypto.randomUUID(), status: 'Tracker comes in a later chunk', finalSubmitted: false }]; }
}

@Component({
  standalone: true,
  imports: [FormsModule, ReactiveFormsModule],
  template: `
    <section class="panel">
      <h2>Scholarship AI</h2>
      @if (providerStatus) {
        <p class="mode-line">Mode: <strong>{{ providerStatus.activeProvider }}</strong> - {{ providerStatus.message }}</p>
      }
      <form [formGroup]="form" (ngSubmit)="analyze()" class="form-grid">
        <input class="wide" formControlName="targetChatPortalUrl" placeholder="DeepSeek chat URL">
        <input class="wide" formControlName="query" placeholder="Paste university program link here">
        @if (profile) {
          <div class="profile-summary wide">
            <span><strong>Profile:</strong> {{ profile.highestDegree }} | CGPA {{ profile.cgpa }} | IELTS {{ profile.ieltsOverall }} / CEFR {{ profile.cefrLevel }}</span>
            <span><strong>Interests:</strong> {{ profile.researchInterests }}</span>
            <span><strong>Scholarships:</strong> {{ profile.scholarshipPreferences }}</span>
            <span><strong>Preferred regions:</strong> {{ profile.preferredRegions }}</span>
          </div>
        }
        @if (isManualDeepSeekMode() && !chatPortalLoggedIn) {
          <button type="button" (click)="loginToChatPortal()">Login to DeepSeek with Google</button>
        }
        <button [disabled]="loading || !providerStatus || providerNeedsKey()">{{ loading ? 'Working...' : (isManualDeepSeekMode() ? 'Analyze with DeepSeek Browser' : 'Analyze') }}</button>
        @if (isManualDeepSeekMode()) {
          <button type="button" class="secondary" (click)="generatePrompt()">Build Prompt Only</button>
        }
      </form>
      @if (providerNeedsKey()) {
        <p class="warning">AI provider is not configured. Add API key or switch to Manual mode.</p>
      }

      @if (loading) { <p>Working...</p> }
      @if (isManualDeepSeekMode() && prompt && (showFormattedPrompt || showManualJsonFallback)) {
        <section class="result">
          @if (showFormattedPrompt) {
            <h3>Formatted Prompt</h3>
            <button type="button" (click)="copyPrompt()">Copy Prompt</button>
            <textarea class="prompt-box" [value]="prompt" readonly></textarea>
          }
          @if (showManualJsonFallback) {
            <h3>Manual Fallback: Paste DeepSeek JSON Response</h3>
            <textarea class="prompt-box" [(ngModel)]="pastedJson" [ngModelOptions]="{standalone: true}" placeholder="Paste strict JSON response here"></textarea>
            <button type="button" (click)="parseResponse()">Validate JSON</button>
          }
        </section>
      }

      @if (parseResult) {
        <section class="result">
          @if (!parseResult.isValid) {
            <div class="warning">
              @for (error of parseResult.errors; track error) { <p>{{ error }}</p> }
            </div>
          }
          @if (programRows().length) {
            <h3>Recommended Programs</h3>
            <table>
              <tr><th>Save</th><th>University</th><th>Program</th><th>Scholarship</th><th>Fee</th><th>Deadline</th><th>English / MOI</th><th>Sources</th><th>Eligibility</th></tr>
              @for (row of programRows(); track key(row.university, row.program)) {
                <tr>
                  <td><input type="checkbox" [(ngModel)]="selected[key(row.university, row.program)]" [ngModelOptions]="{standalone: true}"></td>
                  <td><strong>{{ row.university.universityName }}</strong><br><small>{{ row.university.country }} {{ row.university.city }}</small></td>
                  <td><strong>{{ row.program.programName }}</strong><br><small>{{ row.program.degreeLevel }} | {{ row.program.field }}</small></td>
                  <td>
                    <div class="detail-block">
                      @for (item of scholarshipItems(row.program); track item.label) {
                        <div><strong>{{ item.label }}</strong><span>{{ item.value }}</span></div>
                      }
                    </div>
                  </td>
                  <td>
                    <div class="detail-block">
                      @for (item of feeItems(row.program.tuitionFee); track item.label) {
                        <div><strong>{{ item.label }}</strong><span>{{ item.value }}</span></div>
                      }
                    </div>
                  </td>
                  <td>
                    <span class="status-pill" [class.closed]="deadlineStatus(row.program.deadline) === 'closed'" [class.open]="deadlineStatus(row.program.deadline) === 'open'">
                      {{ deadlineStatus(row.program.deadline) === 'closed' ? 'Closed' : deadlineStatus(row.program.deadline) === 'open' ? 'Open' : 'Check' }}
                    </span>
                    <span class="deadline-text">{{ row.program.deadline }}</span>
                  </td>
                  <td>
                    <div class="language-block">
                      @for (item of languageItems(row.program.language); track item.label) {
                        <div>
                          <strong>{{ item.label }}</strong>
                          <span>{{ item.value }}</span>
                        </div>
                      }
                    </div>
                  </td>
                  <td>@for (src of row.program.sourceUrls; track src) { <a [href]="src" target="_blank">source</a><br> }</td>
                  <td>
                    <div class="eligibility-block">
                      @for (section of eligibilitySections(row.program.eligibility); track section.title) {
                        <div class="eligibility-section">
                          <strong>{{ section.title }}</strong>
                          @for (item of section.items; track item) { <span>{{ item }}</span> }
                        </div>
                      }
                    </div>
                  </td>
                </tr>
              }
            </table>
            <button type="button" (click)="saveSelected()">Save Selected Results</button>
          }
        </section>
      }
      @if (saveResult) { <p class="success">{{ saveResult }}</p> }
    </section>
  `
})
class AnalyzerComponent {
  private fb = inject(FormBuilder);
  private api = inject(ApiService);
  private profileStore = inject(ProfileStore);
  form = this.fb.group({
    targetChatPortalUrl: ['https://chat.deepseek.com/'],
    query: ['']
  });
  pastedJson = '';
  prompt = '';
  parseResult: any;
  providerStatus: any;
  saveResult = '';
  selected: Record<string, boolean> = {};
  loading = false;
  analysis: any;
  profile: any;
  chatPortalLoggedIn = false;
  showFormattedPrompt = false;
  showManualJsonFallback = false;

  constructor() {
    this.api.get<any>('/ai-search/provider-status').subscribe(r => this.providerStatus = r);
    this.loadChatPortalSessionStatus();
    this.profileStore.profile$.subscribe(profile => this.profile = profile);
  }

  analyze() {
    this.parseResult = null;
    this.saveResult = '';
    if (!this.providerStatus) {
      this.saveResult = 'AI provider status is still loading. Try again in a moment.';
      return;
    }
    if (this.isManualDeepSeekMode()) {
      this.runChatPortalSearch();
      return;
    }
    if (this.providerNeedsKey()) {
      this.parseResult = null;
      this.saveResult = 'AI provider is not configured. Add API key or switch to Manual mode.';
      return;
    }
    this.prompt = '';
    this.pastedJson = '';
    this.runConfiguredProvider();
  }

  loginToChatPortal() {
    this.saveResult = 'Opening DeepSeek login window. Finish Google sign-in manually, then this app will save the session.';
    this.api.post<any>('/chat-portal/login', {
      targetChatPortalUrl: this.form.value.targetChatPortalUrl
    }).subscribe(r => {
      this.chatPortalLoggedIn = true;
      this.saveResult = r.message;
    }, err => {
      this.saveResult = err.error?.message || err.error?.title || 'DeepSeek login failed.';
    });
  }

  loadChatPortalSessionStatus() {
    this.api.get<any>('/chat-portal/session-status').subscribe(r => this.chatPortalLoggedIn = !!r.isLoggedIn);
  }

  providerNeedsKey() {
    return this.providerStatus &&
      !this.providerStatus.isManualMode &&
      this.providerStatus.activeProvider !== 'Mock' &&
      !this.providerStatus.apiKeyConfigured;
  }

  isManualDeepSeekMode() {
    return !this.providerStatus ||
      this.providerStatus.isManualMode ||
      this.providerStatus.activeProvider === 'ManualDeepSeek';
  }

  generatePrompt() {
    this.loading = true;
    this.showFormattedPrompt = true;
    this.showManualJsonFallback = true;
    this.api.post<any>('/chat-portal/build-prompt', { universityUrl: this.form.value.query }).subscribe(r => {
      this.loading = false;
      this.prompt = r.prompt;
      this.saveResult = '';
    }, err => { this.loading = false; this.saveResult = err.error?.message || 'Prompt generation failed.'; });
  }

  copyPrompt() {
    navigator.clipboard?.writeText(this.prompt);
  }

  parseResponse() {
    this.loading = true;
    this.api.post<any>('/ai-search/parse-response', { jsonResponse: this.pastedJson }).subscribe(r => {
      this.loading = false;
      this.parseResult = r;
      this.selected = {};
      for (const item of this.programRows()) this.selected[this.key(item.university, item.program)] = true;
    }, err => { this.loading = false; this.parseResult = { isValid: false, errors: [err.error?.message || 'Parse failed.'], universities: [] }; });
  }

  runChatPortalSearch() {
    const universityUrl = this.form.value.query || '';
    if (!universityUrl.trim()) {
      this.saveResult = 'Paste a university program link first.';
      return;
    }

    this.loading = true;
    this.prompt = '';
    this.pastedJson = '';
    this.showFormattedPrompt = false;
    this.showManualJsonFallback = false;
    this.api.post<any>('/chat-portal/query', {
      targetChatPortalUrl: this.form.value.targetChatPortalUrl,
      universityUrl
    }).subscribe(r => {
      this.prompt = r.prompt;
      this.pastedJson = r.result;
      this.parseDeepSeekResult(r.result);
    }, err => {
      this.loading = false;
      this.parseResult = { isValid: false, errors: [err.error?.message || err.error?.title || 'DeepSeek browser automation failed. Login first, then try again.'], universities: [] };
    });
  }

  parseDeepSeekResult(jsonResponse: string) {
    this.api.post<any>('/ai-search/parse-response', { jsonResponse }).subscribe(r => {
      this.loading = false;
      this.parseResult = r;
      this.selected = {};
      for (const item of this.programRows()) this.selected[this.key(item.university, item.program)] = true;
      this.saveResult = r.isValid ? 'DeepSeek response parsed successfully.' : 'DeepSeek returned text, but JSON validation found issues.';
    }, err => {
      this.loading = false;
      this.parseResult = { isValid: false, errors: [err.error?.message || 'DeepSeek response parse failed.'], universities: [] };
    });
  }

  runConfiguredProvider() {
    this.loading = true;
    this.api.post<any>('/ai-search/search', this.criteriaPayload()).subscribe(r => {
      this.loading = false;
      this.parseResult = r;
      this.selected = {};
      for (const item of this.programRows()) this.selected[this.key(item.university, item.program)] = true;
      this.saveResult = r.analysisId
        ? `Analysis saved automatically. Analysis ID: ${r.analysisId}`
        : r.isValid ? '' : 'Configured provider did not return valid results.';
    }, err => {
      this.loading = false;
      this.parseResult = { isValid: false, errors: [err.error?.message || 'Provider search failed.'], universities: [] };
    });
  }

  saveSelected() {
    const keys = Object.keys(this.selected).filter(k => this.selected[k]);
    this.loading = true;
    this.api.post<any>('/ai-search/save-results', {
      criteria: this.criteriaPayload(),
      universities: this.parseResult?.universities || [],
      selectedProgramKeys: keys
    }).subscribe(r => {
      this.loading = false;
      this.saveResult = `${r.savedPrograms} program(s) saved. Analysis ID: ${r.analysisId}`;
    }, err => { this.loading = false; this.saveResult = err.error?.message || 'Save failed.'; });
  }

  programRows() {
    const universities = this.parseResult?.universities || [];
    return universities.flatMap((university: any) => (university.programs || []).map((program: any) => ({ university, program })));
  }

  key(university: any, program: any) {
    return `${university.universityName}|${program.programName}|${program.degreeLevel}`;
  }

  eligibilitySections(value: string) {
    const text = value || 'Not found on official page';
    const parts = text.split(/\s+\|\s+/).map(x => x.trim()).filter(Boolean);
    const sectionMap: Record<string, string[]> = {
      Requirements: [],
      Assessment: [],
      Advantages: [],
      Risks: []
    };

    for (const part of parts) {
      if (/^(Previous degree|Minimum GPA|Entry test)/i.test(part)) sectionMap['Requirements'].push(part);
      else if (/^(Admission chance|Scholarship chance|Summary)/i.test(part)) sectionMap['Assessment'].push(part);
      else if (/^Advantages:/i.test(part)) sectionMap['Advantages'].push(...this.splitInlineList(part.replace(/^Advantages:\s*/i, '')));
      else if (/^Risks:/i.test(part)) sectionMap['Risks'].push(...this.splitInlineList(part.replace(/^Risks:\s*/i, '')));
      else sectionMap['Assessment'].push(part);
    }

    return Object.entries(sectionMap)
      .filter(([, items]) => items.length)
      .map(([title, items]) => ({ title, items }));
  }

  splitInlineList(value: string) {
    return value
      .split(/;\s+(?=[A-Z][A-Za-z ]+:|[A-Z])/)
      .map(x => x.trim())
      .filter(Boolean);
  }

  deadlineStatus(value: string) {
    const text = (value || '').toLowerCase();
    if (/(closed|expired|passed|past|ended|not open)/.test(text)) return 'closed';
    if (/(open|available|active|upcoming|reopening|expected|deadline)/.test(text)) return 'open';
    return 'unknown';
  }

  languageItems(value: string) {
    const text = value || 'Not found on official page';
    const parts = text.split(/\s+\|\s+/).map(x => x.trim()).filter(Boolean);
    const items = parts.map(part => {
      const index = part.indexOf(':');
      if (index < 0) return { label: 'Details', value: part };
      return {
        label: part.slice(0, index).trim(),
        value: part.slice(index + 1).trim()
      };
    });

    return items.length ? items : [{ label: 'Details', value: text }];
  }

  scholarshipItems(program: any) {
    if (!program?.scholarshipsAvailable) return [{ label: 'Scholarship', value: 'Not found on official page' }];
    const parts = (program.scholarshipDetails || '').split(/\s+\|\s+/).map((x: string) => x.trim()).filter(Boolean);
    const labels = ['Name', 'Coverage', 'Amount'];
    return parts.map((part: string, index: number) => {
      const colon = part.indexOf(':');
      if (colon > 0 && /^(deadline|eligibility)$/i.test(part.slice(0, colon).trim())) {
        return { label: part.slice(0, colon).trim(), value: part.slice(colon + 1).trim() };
      }
      return { label: labels[index] || 'Details', value: part };
    });
  }

  feeItems(value: string) {
    const text = value || 'Not found on official page';
    const parts = text.split(/\s+\|\s+/).map(x => x.trim()).filter(Boolean);
    return parts.map(part => {
      const colon = part.indexOf(':');
      if (colon < 0) return { label: 'Details', value: part };
      return {
        label: part.slice(0, colon).trim(),
        value: part.slice(colon + 1).trim()
      };
    });
  }

  criteriaPayload() {
    return {
      query: this.form.value.query || '',
      degreeLevel: 'Masters',
      fieldPreference: this.profile?.researchInterests || this.profile?.skills || '',
      countryPreference: this.profile?.preferredRegions || '',
      scholarshipPreference: this.profile?.scholarshipPreferences || ''
    };
  }

  openProgram(url: string) { if (url) window.open(url, '_blank'); }
}

@Component({
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <section class="panel">
      <h2>Automation Run</h2>
      <p class="warning">Playwright opens the configured chat portal locally and saves the browser session after login.</p>
      <form [formGroup]="form" class="form-grid">
        <input class="wide" formControlName="targetChatPortalUrl" placeholder="Chat portal URL">
        <input class="wide" formControlName="universityUrl" placeholder="University program URL">
        <button type="button" (click)="login()">Login and Save Session</button>
        <button type="button" (click)="run()">Run Research Prompt</button>
      </form>
      @if (prompt) {
        <h3>Formatted Prompt</h3>
        <textarea class="prompt-box" [value]="prompt" readonly></textarea>
      }
      <pre>{{ result }}</pre>
    </section>
  `
})
class AutomationComponent {
  private fb = inject(FormBuilder);
  private api = inject(ApiService);
  form = this.fb.group({
    targetChatPortalUrl: ['https://chat.deepseek.com/'],
    universityUrl: ['']
  });
  result = '';
  prompt = '';

  login() {
    this.result = 'Opening browser for login...';
    this.api.post<any>('/chat-portal/login', {
      targetChatPortalUrl: this.form.value.targetChatPortalUrl
    }).subscribe(r => {
      this.result = r.message;
    }, err => {
      this.result = err.error?.message || err.error?.title || 'Login automation failed.';
    });
  }

  run() {
    this.result = 'Sending prompt to chat portal...';
    this.prompt = '';
    this.api.post<any>('/chat-portal/query', this.form.value).subscribe(r => {
      this.prompt = r.prompt;
      this.result = r.result;
    }, err => {
      this.result = err.error?.message || err.error?.title || 'Chat portal query failed.';
    });
  }
}

@Component({
  standalone: true,
  template: `
    <section class="panel">
      <h2>Review Before Submit</h2>
      <p class="warning">Final submission is intentionally outside automation. Review all fields, documents, warnings, and submit manually on the university portal.</p>
      <ul>
        <li>Filled fields reviewed</li>
        <li>Unfilled fields completed manually</li>
        <li>Low confidence values corrected</li>
        <li>Documents verified</li>
        <li>Official eligibility notes checked</li>
      </ul>
    </section>
  `
})
class ReviewComponent {}

@Component({
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <section class="panel">
      <h2>SOP and Email Generator</h2>
      <form [formGroup]="form" (ngSubmit)="generate()" class="form-grid">
        <select formControlName="contentType"><option>SOP</option><option>Motivation letter</option><option>Professor email</option><option>Scholarship essay</option><option>Short answer</option></select>
        <input formControlName="universityName" placeholder="University">
        <input formControlName="programName" placeholder="Program">
        <textarea class="wide" formControlName="extraContext" placeholder="Specific instructions"></textarea>
        <button>Generate</button>
      </form>
      <textarea class="output" [value]="content"></textarea>
    </section>
  `
})
class WriterComponent {
  private fb = inject(FormBuilder);
  private api = inject(ApiService);
  content = '';
  form = this.fb.group({ contentType: ['SOP'], universityName: [''], programName: [''], extraContext: [''] });
  generate() { this.content = 'AI content generation will be added after the research pipeline chunks.'; }
}

bootstrapApplication(AppComponent, {
  providers: [
    provideHttpClient(withInterceptors([authInterceptor])),
    provideRouter([
      { path: 'login', component: LoginComponent },
      { path: '', component: DashboardComponent, canActivate: [authGuard] },
      { path: 'profile', component: ProfileComponent, canActivate: [authGuard] },
      { path: 'documents', component: DocumentsComponent, canActivate: [authGuard] },
      { path: 'tracker', component: TrackerComponent, canActivate: [authGuard] },
      { path: 'analyzer', component: AnalyzerComponent, canActivate: [authGuard] },
      { path: 'automation', component: AutomationComponent, canActivate: [authGuard] },
      { path: 'review', component: ReviewComponent, canActivate: [authGuard] },
      { path: 'writer', component: WriterComponent, canActivate: [authGuard] }
    ])
  ]
});
