import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EventModel } from './event.interface';
import { EventService } from './event.service';
import { AuthService } from '../../core/services/auth';
import { TitlePage, TitleAction, Breadcrumb, DropdownItem } from '../../shared/layouts/title-page/title-page';
import { ParentChildHeaderSimpleComponent } from '../../shared/components/parent-child-header-simple/parent-child-header-simple.component';
import { ExportUtil } from '../../shared/utils/export.util';

@Component({
  selector: 'app-event',
  standalone: true,
  imports: [CommonModule, FormsModule, TitlePage, ParentChildHeaderSimpleComponent],
  templateUrl: './event.html',
  styleUrl: './event.scss'
})
export class Event implements OnInit {
  events: EventModel[] = [];
  filteredEvents: EventModel[] = [];
  displayedEvents: EventModel[] = [];
  loading = false;
  userRole: string | null = null;
  viewMode: 'grid' | 'list' = 'grid';
  sortBy: string = 'created-desc';
  showSortMenu = false;
  showExportDropdown = false;
  eventsPerPage = 5;
  currentPage = 1;
  searchTerm: string = '';

  breadcrumbs: Breadcrumb[] = [
    { label: 'Dashboard' },
    { label: 'Events' }
  ];

  titleActions: TitleAction[] = [];

  get isParent(): boolean {
    return this.authService.isParent();
  }

  constructor(
    private eventService: EventService,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit() {
    this.userRole = this.authService.getUserRole();
    this.setupTitleActions();
    this.loadEvents();
    this.eventService.events$.subscribe(events => {
      this.events = events;
    });
  }

  setupTitleActions() {
    this.titleActions = [
      {
        label: 'Export',
        class: 'btn btn-light me-2',
        action: () => {},
        dropdown: {
          items: [
            {
              label: 'Export as PDF',
              icon: 'bi bi-file-earmark-pdf',
              action: () => this.exportToPDF()
            },
            {
              label: 'Export as Excel',
              icon: 'bi bi-file-earmark-excel',
              action: () => this.exportToExcel()
            }
          ]
        }
      }
    ];

    if (this.authService.isAdmin() || this.authService.isTeacher()) {
      this.titleActions.push({
        label: 'Add Event',
        class: 'btn btn-primary',
        action: () => this.router.navigate(['/events/add'])
      });
    }
  }

  loadEvents() {
    this.loading = true;
    this.eventService.loadEvents().subscribe({
      next: (events) => {
        this.events = events;
        this.applyFilter();
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading events:', error);
        this.loading = false;
      }
    });
  }

  editEvent(event: EventModel) {
    this.router.navigate(['/events/edit', event.id]);
  }

  deleteEvent(id: number) {
    if (confirm('Are you sure you want to delete this event?')) {
      this.eventService.deleteEvent(id).subscribe({
        next: () => {
          this.loadEvents();
        },
        error: (error) => {
          console.error('Error deleting event:', error);
        }
      });
    }
  }

  canEdit(): boolean {
    return this.authService.isAdmin() || this.authService.isTeacher();
  }

  canDelete(): boolean {
    return this.authService.isAdmin();
  }

  setViewMode(mode: 'grid' | 'list') {
    this.viewMode = mode;
  }

  setSortBy(sortBy: string) {
    this.sortBy = sortBy;
    this.showSortMenu = false;
    this.applySort();
  }

  toggleSortMenu() {
    this.showSortMenu = !this.showSortMenu;
  }

  applyFilter() {
    let filtered = [...this.events];
    
    // Apply search filter
    if (this.searchTerm.trim()) {
      filtered = filtered.filter(event => 
        event.name.toLowerCase().includes(this.searchTerm.toLowerCase())
      );
    }
    
    // Apply sorting
    if (this.isParent) {
      filtered.sort((a, b) => new Date(a.time).getTime() - new Date(b.time).getTime());
    } else {
      switch (this.sortBy) {
        case 'name-asc':
          filtered.sort((a, b) => a.name.localeCompare(b.name));
          break;
        case 'name-desc':
          filtered.sort((a, b) => b.name.localeCompare(a.name));
          break;
        case 'price-asc':
          filtered.sort((a, b) => a.price - b.price);
          break;
        case 'price-desc':
          filtered.sort((a, b) => b.price - a.price);
          break;
        case 'created-desc':
          filtered.sort((a, b) => new Date(b.createdAt || '').getTime() - new Date(a.createdAt || '').getTime());
          break;
        case 'created-asc':
          filtered.sort((a, b) => new Date(a.createdAt || '').getTime() - new Date(b.createdAt || '').getTime());
          break;
      }
    }
    
    this.filteredEvents = filtered;
    this.currentPage = 1;
    this.updateDisplayedEvents();
  }

  applySort() {
    this.applyFilter();
  }

  updateDisplayedEvents() {
    const endIndex = this.currentPage * this.eventsPerPage;
    this.displayedEvents = this.filteredEvents.slice(0, endIndex);
  }

  loadMore() {
    this.currentPage++;
    this.updateDisplayedEvents();
  }

  hasMoreEvents(): boolean {
    return this.displayedEvents.length < this.filteredEvents.length;
  }

  getSortLabel(): string {
    switch (this.sortBy) {
      case 'name-asc': return 'Sort by A-Z';
      case 'name-desc': return 'Sort by Z-A';
      case 'price-asc': return 'Price: Low to High';
      case 'price-desc': return 'Price: High to Low';
      case 'created-desc': return 'Newest First';
      case 'created-asc': return 'Oldest First';
      default: return 'Newest First';
    }
  }

  viewParticipants(event: EventModel) {
    this.router.navigate(['/events', event.id, 'participants']);
  }

  viewEventDetail(event: EventModel) {
    this.router.navigate(['/events/detail', event.id]);
  }

  isEventActive(event: EventModel): boolean {
    const eventDate = new Date(event.time);
    const now = new Date();
    return eventDate > now;
  }

  getEventStatus(event: EventModel): string {
    return this.isEventActive(event) ? 'Active' : 'Expired';
  }

  getEventStatusClass(event: EventModel): string {
    return this.isEventActive(event) ? 'bg-success' : 'bg-danger';
  }

  toggleExportDropdown() {
    this.showExportDropdown = !this.showExportDropdown;
  }

  exportToPDF(): void {
    const data = this.filteredEvents.map(event => ({
      'Name': event.name,
      'Type': event.type,
      'Price': `$${event.price}`,
      'Age Range': `${event.ageFrom}-${event.ageTo} years`,
      'Capacity': event.capacity,
      'Date & Time': new Date(event.time).toLocaleString(),
      'Status': this.getEventStatus(event),
      'Participants': event.participants?.length || 0
    }));

    ExportUtil.exportToPDF(data, 'Events Report');
  }

  exportToExcel(): void {
    const data = this.filteredEvents.map(event => ({
      'Name': event.name,
      'Type': event.type,
      'Price': `$${event.price}`,
      'Age Range': `${event.ageFrom}-${event.ageTo} years`,
      'Capacity': event.capacity,
      'Date & Time': new Date(event.time).toLocaleString(),
      'Status': this.getEventStatus(event),
      'Participants': event.participants?.length || 0
    }));

    ExportUtil.exportToExcel(data, 'Events Report');
  }
}
