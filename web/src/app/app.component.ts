import { Component } from '@angular/core';
import { ShipmentListComponent } from './shipment/shipment-list.component';
import { ChatComponent } from './assistant/chat.component';

@Component({
  selector: 'app-root',
  imports: [ShipmentListComponent, ChatComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})

export class AppComponent {
  title = 'shipments-web';
}
