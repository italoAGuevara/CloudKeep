import { Component } from '@angular/core';
import { Routes } from '@angular/router';
import { DashboardLayoutComponent } from './layout/dashboard-layout.component';
import { HomeComponent } from './pages/home/home.component';
import { JobsComponent } from './pages/jobs/jobs.component';
import { JobWizardComponent } from './pages/jobs/job-wizard/job-wizard.component';
import { DestinationsComponent } from './pages/destinations/destinations.component';
import { ScriptsComponent } from './pages/scripts/scripts.component';
import { SettingsComponent } from './pages/settings/settings.component';
import { JobReportComponent } from './pages/reports/job-report/job-report.component';
import { AboutComponent } from './pages/about/about.component';
import { AboutGeneralComponent } from './pages/about/about-general/about-general.component';
import { AboutLibrariesComponent } from './pages/about/about-libraries/about-libraries.component';
import { LoginComponent } from './pages/login/login.component';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    component: LoginComponent,
  },
  {
    path: '',
    component: DashboardLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', component: HomeComponent },
      { path: 'trabajos', component: JobsComponent },
      { path: 'trabajos/nuevo', component: JobWizardComponent },
      { path: 'trabajos/:id/editar', component: JobWizardComponent },
      { path: 'destinos', component: DestinationsComponent },
      { path: 'scripts', component: ScriptsComponent },
      { path: 'reportes/trabajos', redirectTo: 'reportes', pathMatch: 'full' },
      { path: 'reportes/destinos', redirectTo: 'reportes', pathMatch: 'full' },
      { path: 'reportes/logs', redirectTo: 'reportes', pathMatch: 'full' },
      { path: 'reportes', component: JobReportComponent },
      { path: 'configuracion', component: SettingsComponent },
      {
        path: 'acerca-de',
        component: AboutComponent,
        children: [
          { path: '', redirectTo: 'general', pathMatch: 'full' },
          { path: 'general', component: AboutGeneralComponent },
          { path: 'librerias', component: AboutLibrariesComponent },
        ],
      },
    ],
  },
];
