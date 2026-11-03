import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Shipment } from './shipment.model';

@Injectable({ providedIn: 'root' })
export class ShipmentService {
  private http = inject(HttpClient);

  getAll() {
    return this.http.get<Shipment[]>('/api/shipments');
  }
}