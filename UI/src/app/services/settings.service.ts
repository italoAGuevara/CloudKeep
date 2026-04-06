import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { unwrapApiDetails } from '../utils/api-response.util';

const API_SCRIPT_TIMEOUT = '/api/settings/script-execution-timeout';

export interface ScriptExecutionTimeoutDto {
  scriptExecutionTimeoutMinutes: number;
}

@Injectable({
  providedIn: 'root',
})
export class SettingsService {
  private http = inject(HttpClient);

  getScriptExecutionTimeout(): Observable<number> {
    return this.http.get<ScriptExecutionTimeoutDto | unknown>(API_SCRIPT_TIMEOUT).pipe(
      map((res) => unwrapApiDetails<ScriptExecutionTimeoutDto>(res).scriptExecutionTimeoutMinutes)
    );
  }

  setScriptExecutionTimeout(minutes: number): Observable<void> {
    return this.http.put(API_SCRIPT_TIMEOUT, { scriptExecutionTimeoutMinutes: minutes }).pipe(map(() => undefined));
  }
}
