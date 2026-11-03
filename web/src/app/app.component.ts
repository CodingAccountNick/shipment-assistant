import { Component } from '@angular/core';
import { ShipmentListComponent } from './shipment/shipment-list.component';

@Component({
  selector: 'app-root',
  imports: [ShipmentListComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})

export class AppComponent {
  title = 'shipments-web';
}
