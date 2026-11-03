import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Shipment } from './shipment.model';
import { ShipmentService } from './shipment.service';

@Component({
  selector: 'app-shipment-list',
  standalone: true,
  imports: [DatePipe],
  template: `
    <h2>Shipments</h2>

    @if (loading()) {
      <p>Loading shipments…</p>
    } @else if (error()) {
      <p class="error">{{ error() }}</p>
    } @else {
      <p class="summary">{{ shipments().length }} shipments, {{ deliveredCount() }} delivered</p>

      <table>
        <thead>
          <tr>
            <th>Tracking number</th>
            <th>From</th>
            <th>To</th>
            <th>Status</th>
            <th>Created</th>
          </tr>
        </thead>
        <tbody>
          @for (s of shipments(); track s.trackingNumber) {
            <tr>
              <td>{{ s.trackingNumber }}</td>
              <td>{{ s.origin }}</td>
              <td>{{ s.destination }}</td>
              <td><span [class]="'status status-' + s.status">{{ s.status }}</span></td>
              <td>{{ s.createdAt | date: 'dd MMM yyyy' }}</td>
            </tr>
          } @empty {
            <tr><td colspan="5">No shipments yet.</td></tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: [`
    table { border-collapse: collapse; width: 100%; }
    th, td { text-align: left; padding: 8px 12px; border-bottom: 1px solid #ddd; }
    .status { padding: 2px 8px; border-radius: 12px; font-size: 0.85em; background: #eee; }
    .status-Delivered { background: #d4edda; }
    .status-Delayed { background: #f8d7da; }
    .status-OutForDelivery { background: #fff3cd; }
    .error { color: #b00020; }
    .summary { color: #555; }
  `]
})
export class ShipmentListComponent implements OnInit {
  private shipmentService = inject(ShipmentService);

  // Signals: values the template reads; when they change, the view updates
  shipments = signal<Shipment[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  // A computed signal: recalculated automatically whenever shipments() changes
  deliveredCount = computed(() => this.shipments().filter(s => s.status === 'Delivered').length);

  ngOnInit() {
    this.shipmentService.getAll().subscribe({
      next: data => {
        this.shipments.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load shipments. Is the API running?');
        this.loading.set(false);
      }
    });
  }
}