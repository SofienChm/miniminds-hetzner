import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { EventModel } from '../event.interface';
import { EventService } from '../event.service';
import { TitlePage, Breadcrumb } from '../../../shared/layouts/title-page/title-page';
import { ConfirmationModal } from '../../../shared/components/confirmation-modal/confirmation-modal';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-edit-event',
  imports: [CommonModule, FormsModule, TitlePage, ConfirmationModal],
  standalone: true,
  templateUrl: './edit-event.html',
  styleUrl: './edit-event.scss'
})
export class EditEvent implements OnInit {
  saving = false;
  loading = false;
  eventId: number = 0;
  showCancelModal = false;

  breadcrumbs: Breadcrumb[] = [
    { label: 'Dashboard' },
    { label: 'Events', url: '/events' },
    { label: 'Edit Event' }
  ];

  event: EventModel = {
    name: '',
    type: '',
    description: '',
    price: 0,
    ageFrom: 0,
    ageTo: 0,
    capacity: 0,
    time: ''
  };

  eventDate: string = '';
  eventTime: string = '';
  imagePreview: string | null = null;
  selectedImage: File | null = null;

  eventTypes = [
    'Workshop',
    'Party',
    'Educational',
    'Sports',
    'Arts & Crafts',
    'Music',
    'Outdoor',
    'Special Event'
  ];

  constructor(
    private eventService: EventService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit() {
    this.eventId = Number(this.route.snapshot.paramMap.get('id'));
    this.loadEvent();
  }

  loadEvent() {
    this.loading = true;
    this.eventService.getEvent(this.eventId).subscribe({
      next: (event) => {
        this.event = { ...event };
        
        // Split existing datetime into date and time parts
        if (event.time) {
          // Try parsing as ISO string first
          let eventDateTime = new Date(event.time);
          
          // If invalid, try parsing as date-only string
          if (isNaN(eventDateTime.getTime())) {
            eventDateTime = new Date(event.time + 'T00:00:00');
          }
          
          if (!isNaN(eventDateTime.getTime())) {
            // Get date in YYYY-MM-DD format
            const year = eventDateTime.getFullYear();
            const month = String(eventDateTime.getMonth() + 1).padStart(2, '0');
            const day = String(eventDateTime.getDate()).padStart(2, '0');
            this.eventDate = `${year}-${month}-${day}`;
            
            // Get time in HH:MM format
            const hours = String(eventDateTime.getHours()).padStart(2, '0');
            const minutes = String(eventDateTime.getMinutes()).padStart(2, '0');
            this.eventTime = `${hours}:${minutes}`;
          }
        }
        
        // Set image preview if event has image
        if (event.image) {
          this.imagePreview = event.image;
        }
        
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading event:', error);
        this.loading = false;
        this.router.navigate(['/events']);
      }
    });
  }

  updateEvent() {
    if (!this.eventDate || !this.eventTime) {
      Swal.fire({
        icon: 'error',
        title: 'Error!',
        text: 'Please select both date and time',
        confirmButtonColor: '#d33'
      });
      return;
    }
    
    this.saving = true;
    
    // Combine date and time into ISO string
    const combinedDateTime = `${this.eventDate}T${this.eventTime}:00`;
    let eventToUpdate = { ...this.event, time: combinedDateTime };
    
    // Add image if selected or keep existing
    if (this.selectedImage) {
      const reader = new FileReader();
      reader.onload = () => {
        eventToUpdate.image = reader.result as string;
        this.submitUpdate(eventToUpdate);
      };
      reader.readAsDataURL(this.selectedImage);
    } else {
      // Keep existing image or remove if cleared
      eventToUpdate.image = this.imagePreview || undefined;
      this.submitUpdate(eventToUpdate);
    }
  }

  private submitUpdate(eventToUpdate: any) {
    // Clean the event object to remove properties that shouldn't be sent
    const cleanEvent = {
      id: eventToUpdate.id,
      name: eventToUpdate.name,
      type: eventToUpdate.type,
      description: eventToUpdate.description,
      price: eventToUpdate.price,
      ageFrom: eventToUpdate.ageFrom,
      ageTo: eventToUpdate.ageTo,
      capacity: eventToUpdate.capacity,
      time: eventToUpdate.time,
      place: eventToUpdate.place,
      image: eventToUpdate.image
    };
    
    this.eventService.updateEvent(cleanEvent).subscribe({
      next: () => {
        this.saving = false;
        Swal.fire({
          icon: 'success',
          title: 'Success!',
          text: 'Event updated successfully',
          confirmButtonColor: '#3085d6'
        }).then(() => {
          this.router.navigate(['/events']);
        });
      },
      error: (error) => {
        console.error('Error updating event:', error);
        this.saving = false;
        Swal.fire({
          icon: 'error',
          title: 'Error!',
          text: 'Failed to update event. Please try again.',
          confirmButtonColor: '#d33'
        });
      }
    });
  }

  onImageSelected(event: any) {
    const file = event.target.files[0];
    if (file) {
      this.selectedImage = file;
      const reader = new FileReader();
      reader.onload = (e: any) => {
        this.imagePreview = e.target.result;
      };
      reader.readAsDataURL(file);
    }
  }

  removeImage() {
    this.imagePreview = null;
    this.selectedImage = null;
    // Clear the file input
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    if (fileInput) {
      fileInput.value = '';
    }
  }

  cancel() {
    this.showCancelModal = true;
  }

  confirmCancel() {
    this.showCancelModal = false;
    this.router.navigate(['/events']);
  }

  closeCancelModal() {
    this.showCancelModal = false;
  }
}
