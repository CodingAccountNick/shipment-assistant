// Matches ShipmentResponse from the .NET API
export type ShipmentStatus = 'Created' | 'InTransit' | 'OutForDelivery' | 'Delivered' | 'Delayed';

export interface Shipment {
  trackingNumber: string;
  origin: string;
  destination: string;
  status: ShipmentStatus;
  createdAt: string;
  deliveredAt: string | null;
}