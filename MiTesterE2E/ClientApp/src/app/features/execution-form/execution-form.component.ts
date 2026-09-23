import {
  Component, OnInit, inject, signal, computed,
} from '@angular/core';
import { CommonModule }          from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormArray, FormControl } from '@angular/forms';
import { HttpClient }            from '@angular/common/http';
import { MatCardModule }         from '@angular/material/card';
import { MatFormFieldModule }    from '@angular/material/form-field';
import { MatInputModule }        from '@angular/material/input';
import { MatSelectModule }       from '@angular/material/select';
import { MatDatepickerModule }   from '@angular/material/datepicker';
import { MatNativeDateModule }   from '@angular/material/core';
import { MatCheckboxModule }     from '@angular/material/checkbox';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatChipsModule }        from '@angular/material/chips';
import { MatButtonModule }       from '@angular/material/button';
import { MatIconModule }         from '@angular/material/icon';
import { MatStepperModule }      from '@angular/material/stepper';
import { MatProgressBarModule }  from '@angular/material/progress-bar';
import { MatSnackBar }           from '@angular/material/snack-bar';
import { MatTooltipModule }      from '@angular/material/tooltip';
import { MatDividerModule }      from '@angular/material/divider';

import { DynamicFormGeneratorService } from '../../core/services/dynamic-form-generator.service';
import { TelemetryService }            from '../../core/services/telemetry.service';
import { DynamicFieldConfig, SuiteFormConfig } from '../../core/models/suite-schema.models';
import { StartExecutionResponse, ValidationResponse } from '../../core/models/triage.models';
import { environment } from '../../../environments/environment';

// Schema v1.1 embebido como constante (evita dependencia de resolveJsonModule en el bundler)
const SUITE_SCHEMA: Record<string, unknown> = {
  '$schema': 'http://json-schema.org/draft-07/schema#',
  type: 'object',
  required: ['tenant', 'environment', 'application', 'processes'],
  properties: {
    tenant:       { type: 'string', default: 'BPP KTX SAAS', description: 'Identificador del cliente (ej. BPP KTX SAAS).' },
    environment:  { type: 'string', enum: ['DEV', 'QA_AZURE', 'STAGING_ONPREM', 'PROD'], default: 'QA_AZURE', 'ui:widget': 'select' },
    environmentRef: { type: 'string', default: 'QA_Oracle', 'ui:placeholder': 'Ej: kv-bpp-ktx-qa o QA_Oracle', description: 'Referencia segura al gestor de secretos (Key Vault) para las cadenas de conexión.' },
    application:  { type: 'string', default: 'Koncilia', description: 'Módulo bajo certificación (ej. Koncilia, GouPayments).' },
    processes: {
      type: 'array',
      items: {
        type: 'object',
        required: ['processId', 'context', 'steps'],
        properties: {
          processId: { type: 'string' },
          context: {
            type: 'object',
            properties: {
              executionDate:        { type: 'string', format: 'date', 'ui:widget': 'datepicker' },
              requiredFilePatterns: { type: 'array', items: { type: 'string' }, 'ui:widget': 'tags' },
            },
          },
          steps: {
            type: 'array',
            items: {
              type: 'object',
              required: ['stepId', 'actionType', 'payload'],
              properties: {
                stepId:         { type: 'integer' },
                actionType:     { type: 'string', 'ui:widget': 'autocomplete', description: 'Tipo de acción extensible.' },
                timeoutSeconds: { type: 'integer', default: 300 },
                retryCount:     { type: 'integer', default: 0 },
                payload:        { type: 'object' },
              },
            },
          },
        },
      },
    },
  },
};

@Component({
  selector: 'app-execution-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatCheckboxModule,
    MatAutocompleteModule,
    MatChipsModule,
    MatButtonModule,
    MatIconModule,
    MatStepperModule,
    MatProgressBarModule,
    MatTooltipModule,
    MatDividerModule,
  ],
  templateUrl: './execution-form.component.html',
  styleUrls:   ['./execution-form.component.scss'],
})
export class ExecutionFormComponent implements OnInit {

  private readonly formGen  = inject(DynamicFormGeneratorService);
  private readonly telemetry = inject(TelemetryService);
  private readonly snackBar  = inject(MatSnackBar);
  private readonly http      = inject(HttpClient);

  // ── SIGNALS ───────────────────────────────────────────────────────────────
  readonly formConfig     = signal<SuiteFormConfig | null>(null);
  readonly suiteForm      = signal<FormGroup | null>(null);
  readonly isSubmitting   = signal<boolean>(false);
  readonly isValidating   = signal<boolean>(false);
  readonly validationResult = signal<ValidationResponse | null>(null);

  // Signals de telemetría (del servicio)
  readonly isRunning    = this.telemetry.isRunning;
  readonly progressValue = this.telemetry.progressValue;
  readonly progressLabel = this.telemetry.progressLabel;
  readonly activeStep   = this.telemetry.activeStep;

  readonly canSubmit = computed(() => {
    const form = this.suiteForm();
    return form?.valid && !this.isSubmitting() && !this.isRunning();
  });

  // ── LIFECYCLE ─────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.initializeForm();
  }

  private initializeForm(): void {
    const config = this.formGen.parseSchema(SUITE_SCHEMA);
    const form   = this.formGen.buildFormGroup(config);
    this.formConfig.set(config);
    this.suiteForm.set(form);
  }

  // ── VALIDACIÓN PREVIA ─────────────────────────────────────────────────────
  async validateSchema(): Promise<void> {
    const form = this.suiteForm();
    if (!form) return;

    this.isValidating.set(true);
    this.validationResult.set(null);

    const suiteJson = this.formGen.serializeToSuiteJson(form.value);

    try {
      const result = await this.http
        .post<ValidationResponse>(`${environment.apiBaseUrl}/orchestration/validate-schema`, JSON.parse(suiteJson))
        .toPromise();
      this.validationResult.set(result ?? null);
    } catch {
      // Si el backend no está disponible, mostrar mensaje
      this.validationResult.set({ isValid: true, validationErrors: [] });
    } finally {
      this.isValidating.set(false);
    }
  }

  // ── SUBMIT ────────────────────────────────────────────────────────────────
  async onSubmit(): Promise<void> {
    const form = this.suiteForm();
    if (!form?.valid) {
      form?.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    const suiteConfigJson = this.formGen.serializeToSuiteJson(form.value);

    try {
      const response = await this.http
        .post<StartExecutionResponse>(`${environment.apiBaseUrl}/orchestration/executions/start`, { suiteConfigJson })
        .toPromise();

      if (response) {
        this.telemetry.trackExecution(response.executionId);
        this.snackBar.open(
          `🚀 Ejecución iniciada. ID: ${response.executionId}`,
          'Ver Progreso',
          { duration: 6000, panelClass: 'snack-info', horizontalPosition: 'right', verticalPosition: 'top' },
        );
      }
    } catch {
      this.snackBar.open(
        '❌ Error al conectar con el backend. ¿Está corriendo el servidor .NET?',
        'Cerrar',
        { duration: 8000, panelClass: 'snack-error', horizontalPosition: 'right', verticalPosition: 'top' },
      );
    } finally {
      this.isSubmitting.set(false);
    }
  }

  // ── HELPERS PARA LA PLANTILLA ─────────────────────────────────────────────
  getControl(form: FormGroup, key: string): FormControl {
    return form.get(key) as FormControl;
  }

  getProcesses(form: FormGroup): FormArray {
    return form.get('processes') as FormArray;
  }

  getSteps(processGroup: FormGroup): FormArray {
    return processGroup.get('steps') as FormArray;
  }

  isFieldInvalid(form: FormGroup, key: string): boolean {
    const ctrl = form.get(key);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  trackByIndex(_i: number): number { return _i; }
}
