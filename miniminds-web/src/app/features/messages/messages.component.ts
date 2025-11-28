import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MessagesService, MailMessage, Recipient } from '../../core/services/messages.service';
import { AuthService } from '../../core/services/auth';
import { Location } from '@angular/common';
import { TitlePage } from "../../shared/layouts/title-page/title-page";
import { ParentChildHeaderSimpleComponent } from '../../shared/components/parent-child-header-simple/parent-child-header-simple.component';

@Component({
  selector: 'app-messages',
  standalone: true,
  imports: [CommonModule, FormsModule, TitlePage, ParentChildHeaderSimpleComponent],
  templateUrl: './messages.component.html',
  styleUrl: './messages.component.scss'
})
export class MessagesComponent implements OnInit {
  activeTab: 'received' | 'sent' = 'received';
  inbox: MailMessage[] = [];
  sent: MailMessage[] = [];
  selectedMessage: any = null;
  isAdmin = false;
  currentUserId = '';
  showNewMessageModal = false;
  showModal = false;

  composeForm = {
    recipientType: 'individual',
    recipientId: '',
    subject: '',
    content: ''
  };
  
  recipients: { parents: Recipient[], teachers: Recipient[] } = { parents: [], teachers: [] };

  get activeMessages(): MailMessage[] {
    return this.activeTab === 'received' ? this.inbox : this.sent;
  }

  back() {
    this.location.back();
  }

  constructor(
    private messagesService: MessagesService,
    private authService: AuthService,
    private location: Location
  ) {}

  ngOnInit(): void {
    this.currentUserId = this.getCurrentUserId();
    this.isAdmin = this.getUserRole() === 'Admin';
    this.loadInbox();
    this.loadSent();
    if (this.isAdmin) {
      this.loadRecipients();
    }
  }

  private getCurrentUserId(): string {
    const token = localStorage.getItem('token');
    if (!token) return '';
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const claims = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];
      return Array.isArray(claims) ? claims[claims.length - 1] : claims || '';
    } catch {
      return '';
    }
  }

  private getUserRole(): string {
    const token = localStorage.getItem('token');
    if (!token) return '';
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || '';
    } catch {
      return '';
    }
  }

  loadInbox(): void {
    this.messagesService.getInbox().subscribe({
      next: (messages) => this.inbox = messages,
      error: (err) => console.error('Error loading inbox:', err)
    });
  }

  loadSent(): void {
    this.messagesService.getSent().subscribe({
      next: (messages) => this.sent = messages,
      error: (err) => console.error('Error loading sent:', err)
    });
  }

  loadRecipients(): void {
    this.messagesService.getRecipients().subscribe({
      next: (data) => this.recipients = data,
      error: (err) => console.error('Error loading recipients:', err)
    });
  }

  switchTab(tab: 'received' | 'sent'): void {
    this.activeTab = tab;
    if (tab === 'received') this.loadInbox();
    if (tab === 'sent') this.loadSent();
  }

  selectMessage(message: MailMessage): void {
    this.messagesService.getMessage(message.id).subscribe({
      next: (data) => {
        this.selectedMessage = data;
        this.showModal = true;
      },
      error: (err) => console.error('Error loading message:', err)
    });
  }

  resetComposeForm(): void {
    this.composeForm = {
      recipientType: 'individual',
      recipientId: '',
      subject: '',
      content: ''
    };
  }

  sendMessage(): void {
    if (!this.composeForm.subject || !this.composeForm.content) return;
    
    this.messagesService.sendMessage(this.composeForm).subscribe({
      next: () => {
        this.resetComposeForm();
        this.closeNewMessageModal();
        this.loadSent();
        this.activeTab = 'sent';
      },
      error: (err) => console.error('Error sending message:', err)
    });
  }

  reply(): void {
    this.composeForm = {
      recipientType: 'individual',
      recipientId: this.selectedMessage.senderId,
      subject: 'Re: ' + this.selectedMessage.subject,
      content: ''
    };
    this.closeModal();
    if (this.isParent) {
      this.openNewMessageModal();
    }
  }

  getRecipientsList(): Recipient[] {
    return [...this.recipients.parents, ...this.recipients.teachers];
  }
  get isParent(): boolean {
      return this.authService.isParent();
  }
  openNewMessageModal(): void {
    this.showNewMessageModal = true;
  }

  closeNewMessageModal(): void {
    this.showNewMessageModal = false;
  }

  closeModal(): void {
    this.showModal = false;
    this.selectedMessage = null;
  }
}
