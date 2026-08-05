import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LoginPanel } from './login-panel';

describe('LoginPanel', () => {
  let component: LoginPanel;
  let fixture: ComponentFixture<LoginPanel>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginPanel],
    }).compileComponents();

    fixture = TestBed.createComponent(LoginPanel);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
